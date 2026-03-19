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

        new ManualResetEvent(false).WaitOne();
    }

    private static void TrackFocus(object? state)
    {
        try
        {
            IntPtr handle = GetForegroundWindow();
            if (handle == IntPtr.Zero) return;

            GetWindowThreadProcessId(handle, out uint pid);
            using var proc = Process.GetProcessById((int)pid);
            string currentApp = proc.ProcessName + ".exe";

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