
#Requires -RunAsAdministrator

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ExePath = Join-Path $ScriptDir "dist\activitytracker_app.exe"
$StartupFolder = [Environment]::GetFolderPath([Environment+SpecialFolder]::Startup)
$ShortcutPath = Join-Path $StartupFolder "ActivityTracker.lnk"

if (-not (Test-Path $ExePath)) {
    Write-Error "Fehler: Die Datei '$ExePath' wurde nicht gefunden."
    Write-Output "Stelle sicher, dass die App gebaut und im dist-Ordner vorhanden ist."
    exit 1
}

$WshShell = New-Object -ComObject WScript.Shell
$Shortcut = $WshShell.CreateShortcut($ShortcutPath)

$Shortcut.TargetPath = $ExePath
$Shortcut.Arguments = "--minimized"
$Shortcut.Description = "ActivityTracker Dashboard - Lädt minimiert im System-Tray"
$Shortcut.WindowStyle = 7

$Shortcut.Save()

Write-Output "✓ Autostart-Verknüpfung erfolgreich erstellt:"
Write-Output "  Ziel:     $ExePath"
Write-Output "  Argument: --minimized"
Write-Output "  Pfad:     $ShortcutPath"
