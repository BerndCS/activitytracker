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

    private static readonly HashSet<string> ActiveApps = new(StringComparer.OrdinalIgnoreCase);
    private static System.Threading.Timer? _timer;
    private static readonly object _lock = new();

    private static readonly HashSet<string> Blacklist = new(StringComparer.OrdinalIgnoreCase)
    {
        "activitytracker_app.exe", "python.exe", "pythonw.exe",
        "tracetimecollector.exe",
        "conhost.exe", "dllhost.exe", "runtimebroker.exe", "svchost.exe",
        "searchhost.exe", "shellexperiencehost.exe", "taskhostw.exe",
        "dwm.exe", "fontdrvhost.exe", "lsass.exe", "csrss.exe",
        "smss.exe", "wininit.exe", "services.exe", "winlogon.exe"
    };

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetConsoleWindow();

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private static void Main()
    {
        HideConsoleWindow();
        SetAutostart();
        InitializeDatabase();

        lock (_lock)
        {
            RecordInitialRunningApps();
        }

        _timer = new System.Threading.Timer(TrackFocus, null, 0, 5000);

        AppDomain.CurrentDomain.ProcessExit += (_, _) => Shutdown();

        new ManualResetEvent(false).WaitOne();
    }

    private static void TrackFocus(object? state)
    {
        if (!Monitor.TryEnter(_lock)) return;
        try
        {
            SyncRunningApps();
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
            DateTime now = DateTime.UtcNow;
            foreach (string app in ActiveApps)
            {
                InsertLog(app, "STOP", now);
            }
            ActiveApps.Clear();
        }
    }

    private static void RecordInitialRunningApps()
    {
        DateTime now = DateTime.UtcNow;
        foreach (string app in GetRunningApps())
        {
            ActiveApps.Add(app);
            InsertLog(app, "START", now);
        }
    }

    private static void SyncRunningApps()
    {
        DateTime now = DateTime.UtcNow;
        HashSet<string> currentApps = GetRunningApps();

        foreach (string app in currentApps)
        {
            if (!ActiveApps.Contains(app))
            {
                InsertLog(app, "START", now);
            }
        }

        foreach (string app in ActiveApps)
        {
            if (!currentApps.Contains(app))
            {
                InsertLog(app, "STOP", now);
            }
        }

        ActiveApps.Clear();
        foreach (string app in currentApps)
        {
            ActiveApps.Add(app);
        }
    }

    private static HashSet<string> GetRunningApps()
    {
        HashSet<string> apps = new(StringComparer.OrdinalIgnoreCase);
        foreach (Process process in Process.GetProcesses())
        {
            try
            {
                if (process.HasExited) continue;

                string appName = process.ProcessName + ".exe";
                if (Blacklist.Contains(appName)) continue;

                apps.Add(appName);
            }
            catch
            {
                // Zugriff auf einzelne Prozesse kann fehlschlagen, wird ignoriert.
            }
            finally
            {
                process.Dispose();
            }
        }

        return apps;
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