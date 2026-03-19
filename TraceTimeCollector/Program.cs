using System.Management;
using System.Runtime.InteropServices;
using Microsoft.Data.Sqlite;
using Microsoft.Win32;

namespace TraceTimeCollector;

internal static class Program
{
	private const string DatabaseFileName = "activity_log.db";
	private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
	private const string RunValueName = "TraceTimeCollector";

	private static readonly string DatabasePath = Path.Combine(AppContext.BaseDirectory, DatabaseFileName);
	private static readonly string ConnectionString = $"Data Source={DatabasePath}";

	private static ManagementEventWatcher? _processStartWatcher;
	private static ManagementEventWatcher? _processStopWatcher;

	private static void Main()
	{
		HideConsoleWindow();
		SetAutostart();
		InitializeDatabase();
		StartProcessWatchers();

		AppDomain.CurrentDomain.ProcessExit += (_, _) => StopProcessWatchers();

		// Keep process alive indefinitely while WMI watchers run in the background.
		using var waitHandle = new ManualResetEvent(false);
		waitHandle.WaitOne();
	}

	private static void InitializeDatabase()
	{
		using var connection = new SqliteConnection(ConnectionString);
		connection.Open();

		using var command = connection.CreateCommand();
		command.CommandText = @"
			CREATE TABLE IF NOT EXISTS Logs (
				Id INTEGER PRIMARY KEY,
				AppName TEXT NOT NULL,
				Action TEXT NOT NULL CHECK(Action IN ('START', 'STOP')),
				Timestamp DATETIME NOT NULL
			);";
		command.ExecuteNonQuery();
	}

	private static void StartProcessWatchers()
	{
		var scope = new ManagementScope(@"\\.\root\CIMV2");
		scope.Connect();

		var startQuery = new WqlEventQuery(
			"SELECT * FROM __InstanceCreationEvent WITHIN 1 WHERE TargetInstance ISA 'Win32_Process'");
		_processStartWatcher = new ManagementEventWatcher(scope, startQuery);
		_processStartWatcher.EventArrived += (_, eventArgs) => HandleProcessEvent(eventArgs, "START");
		_processStartWatcher.Start();

		var stopQuery = new WqlEventQuery(
			"SELECT * FROM __InstanceDeletionEvent WITHIN 1 WHERE TargetInstance ISA 'Win32_Process'");
		_processStopWatcher = new ManagementEventWatcher(scope, stopQuery);
		_processStopWatcher.EventArrived += (_, eventArgs) => HandleProcessEvent(eventArgs, "STOP");
		_processStopWatcher.Start();
	}

	private static void StopProcessWatchers()
	{
		try
		{
			_processStartWatcher?.Stop();
			_processStartWatcher?.Dispose();
		}
		catch
		{
			// Best-effort cleanup on shutdown.
		}

		try
		{
			_processStopWatcher?.Stop();
			_processStopWatcher?.Dispose();
		}
		catch
		{
			// Best-effort cleanup on shutdown.
		}
	}

	private static void HandleProcessEvent(EventArrivedEventArgs eventArgs, string action)
	{
		try
		{
			var targetInstance = (ManagementBaseObject?)eventArgs.NewEvent?["TargetInstance"];
			var appName = targetInstance?["Name"]?.ToString();
			if (string.IsNullOrWhiteSpace(appName))
			{
				return;
			}

			InsertLog(appName, action, DateTime.UtcNow);
		}
		catch
		{
			// Swallow event processing errors so monitoring can continue.
		}
	}

	private static void InsertLog(string appName, string action, DateTime timestamp)
	{
		using var connection = new SqliteConnection(ConnectionString);
		connection.Open();

		using var command = connection.CreateCommand();
		command.CommandText = @"
			INSERT INTO Logs (AppName, Action, Timestamp)
			VALUES ($appName, $action, $timestamp);";
		command.Parameters.AddWithValue("$appName", appName);
		command.Parameters.AddWithValue("$action", action);
		command.Parameters.AddWithValue("$timestamp", timestamp.ToString("o"));
		command.ExecuteNonQuery();
	}

	private static void SetAutostart()
	{
		var executablePath = Environment.ProcessPath;
		if (string.IsNullOrWhiteSpace(executablePath))
		{
			return;
		}

		using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, true);
		key?.SetValue(RunValueName, executablePath);
	}

	private static void HideConsoleWindow()
	{
		var handle = GetConsoleWindow();
		if (handle != IntPtr.Zero)
		{
			ShowWindow(handle, 0);
		}
	}

	[DllImport("kernel32.dll")]
	private static extern IntPtr GetConsoleWindow();

	[DllImport("user32.dll")]
	private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
}
