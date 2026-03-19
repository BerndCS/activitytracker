# activitytracker

Tracks how long and when desktop programs are used on Windows.

## Components

- `TraceTimeCollector.exe` (C#): collects foreground app activity in the background.
- `activitytracker_app.exe` (PyQt6): tray/dashboard app for live view and control.

## Data Location

- SQLite DB: `%APPDATA%\TraceTime\activity_log.db`

## Build

```powershell
.\Build.ps1 -Clean
```

Optional (for GitHub release candidate asset):

```powershell
.\Build.ps1 -Clean -CreateReleaseBundle
```

This creates `dist\activitytracker_bundle.zip`.

## Update From GitHub Release Candidate

```powershell
.\Update.ps1 -RepoOwner <owner> -RepoName <repo>
```

The updater stops both processes, downloads the latest RC bundle, replaces EXEs, restarts both apps, and keeps the existing SQLite data.
