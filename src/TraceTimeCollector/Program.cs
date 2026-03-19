using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Data.Sqlite;
using Microsoft.Win32;

namespace TraceTimeCollector;

internal static class Program
{
    private const string DatabaseFileName = "activity_log.db";
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName = "TraceTimeCollector";

    private static readonly string DbFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TraceTime");
    private static readonly string DatabasePath = Path.Combine(DbFolder, DatabaseFileName);
    private static readonly string ConnectionString = $"Data Source={DatabasePath}";

    private static string _lastApp = "";
    private static System.Threading.Timer? _timer;
    private static readonly object _lock = new();

    // Prozesse, die nicht als aktive Nutzung gewertet werden sollen
    private static readonly HashSet<string> Blacklist = new(StringComparer.OrdinalIgnoreCase)
    {
        "activitytracker_app.exe", "python.exe", "pythonw.exe",
        "tracetimecollector.exe",
        "conhost.exe", "dllhost.exe", "runtimebroker.exe", "svchost.exe",
        "searchhost.exe", "shellexperiencehost.exe", "taskhostw.exe",
        "dwm.exe", "fontdrvhost.exe", "lsass.exe", "csrss.exe",
        "smss.exe", "wininit.exe", "services.exe", "winlogon.exe"
    };

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetConsoleWindow();

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private static void Main()
    {
        HideConsoleWindow();
        SetAutostart();
        InitializeDatabase();

        _timer = new System.Threading.Timer(TrackFocus, null, 0, 5000);

        // Beim Beenden letzten STOP schreiben
        AppDomain.CurrentDomain.ProcessExit += (_, _) => Shutdown();

        new ManualResetEvent(false).WaitOne();
    }

    private static void TrackFocus(object? state)
    {
        // Lock verhindert, dass zwei Timer-Callbacks gleichzeitig laufen
        if (!Monitor.TryEnter(_lock)) return;
        try
        {
            IntPtr handle = GetForegroundWindow();
            if (handle == IntPtr.Zero) return;

            GetWindowThreadProcessId(handle, out uint pid);
            using var proc = Process.GetProcessById((int)pid);
            string currentApp = proc.ProcessName + ".exe";

            // Blacklist: System- und eigene Prozesse ignorieren
            if (Blacklist.Contains(currentApp)) return;

            if (currentApp != _lastApp)
            {
                DateTime now = DateTime.UtcNow;

                if (!string.IsNullOrEmpty(_lastApp))
                {
                    InsertLog(_lastApp, "STOP", now);
                }

                InsertLog(currentApp, "START", now);
                _lastApp = currentApp;
            }
        }
        catch { }
        finally
        {
            Monitor.Exit(_lock);
        }
    }

    private static void Shutdown()
    {
        _timer?.Dispose();
        lock (_lock)
        {
            if (!string.IsNullOrEmpty(_lastApp))
            {
                InsertLog(_lastApp, "STOP", DateTime.UtcNow);
            }
        }
    }

    private static void InitializeDatabase()
    {
        if (!Directory.Exists(DbFolder)) Directory.CreateDirectory(DbFolder);
        using var connection = new SqliteConnection(ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = @"
            CREATE TABLE IF NOT EXISTS Logs (
                Id INTEGER PRIMARY KEY,
                AppName TEXT NOT NULL,
                Action TEXT NOT NULL,
                Timestamp DATETIME NOT NULL
            );";
        command.ExecuteNonQuery();
    }

    private static void InsertLog(string appName, string action, DateTime timestamp)
    {
        try
        {
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "INSERT INTO Logs (AppName, Action, Timestamp) VALUES ($name, $action, $time)";
            command.Parameters.AddWithValue("$name", appName);
            command.Parameters.AddWithValue("$action", action);
            command.Parameters.AddWithValue("$time", timestamp.ToString("o")); 
            command.ExecuteNonQuery();
        }
        catch {  }
    }

    private static void SetAutostart()
    {
        try
        {
            string? exePath = Process.GetCurrentProcess().MainModule?.FileName;
            if (exePath != null)
            {
                using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, true);
                key?.SetValue(RunValueName, exePath);
            }
        }
        catch { }
    }

    private static void HideConsoleWindow()
    {
        var handle = GetConsoleWindow();
        if (handle != IntPtr.Zero) ShowWindow(handle, 0); 
    }
}