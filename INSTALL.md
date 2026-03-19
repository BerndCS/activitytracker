# ActivityTracker - Installation & Build

## Voraussetzungen

- **Python 3.9+** (mit pip)
- **.NET 8.0 SDK** (für C# Collector)
- **PowerShell 5.1+**

## Installation aus GitHub

### 1. Repository klonen
```powershell
git clone https://github.com/your-repo/activitytracker.git
cd activitytracker
```

### 2. Build durchführen

Führe das Build-Skript aus:
```powershell
.\Build.ps1
```

Das Skript:
- ✓ Installiert Python-Dependencies
- ✓ Konvertiert Python → .exe mit PyInstaller
- ✓ Baut C# Collector
- ✓ Legt beide .exe im `dist`-Ordner ab
- ✓ Optional: Erstellt ein Release-Bundle für GitHub RCs

**Optional: Vollständiger Clean-Build**
```powershell
.\Build.ps1 -Clean
```

**Optional: Release-Bundle für GitHub erstellen**
```powershell
.\Build.ps1 -Clean -CreateReleaseBundle
```

Dadurch entsteht im `dist`-Ordner die Datei `activitytracker_bundle.zip`, die als Asset in einen GitHub Release Candidate hochgeladen werden kann.

## Update aus GitHub Release Candidate

Das Update-Skript lädt den neuesten Release Candidate (Tag enthält `rc`) von GitHub, ersetzt beide EXE-Dateien und startet das Programm neu.

```powershell
.\Update.ps1 -RepoOwner <owner> -RepoName <repo>
```

Beispiel:
```powershell
.\Update.ps1 -RepoOwner larsc -RepoName activitytracker
```

Hinweise:
- Das Skript sucht standardmäßig nach dem Asset `activitytracker_bundle.zip`.
- Die Datenbank bleibt erhalten, da sie unter `%APPDATA%\TraceTime\activity_log.db` liegt.
- Falls kein RC gefunden wird, wird auf Wunsch der letzte stabile Release verwendet.

### 3. Autostart einrichten

```powershell
.\Create-AutostartShortcut.ps1
```

**Hinweis:** Erhfordert Administratorrechte!

## Dateistruktur nach Build

```
dist/
├── activitytracker_app.exe    (Dashboard - PyQt6)
└── TraceTimeCollector.exe     (Collector - C#)
```

## Manuelle Installation

Falls du die Setup-Skripte nicht nutzen möchtest:

1. **Dashboard starten:**
   ```powershell
   .\dist\activitytracker_app.exe --minimized
   ```

2. **Collector starten:**
   ```powershell
   .\dist\TraceTimeCollector.exe
   ```

## Datenbank-Speicherort

Alle Aktivitätsdaten werden gespeichert in:
```
%APPDATA%\TraceTime\activity_log.db
```

Windows-Explorer:
```
C:\Users\<YourUsername>\AppData\Roaming\TraceTime\activity_log.db
```

## Troubleshooting

### PyInstaller findet Module nicht
```powershell
pip install --upgrade PyQt6 pandas psutil
.\Build.ps1 -Clean
```

### .NET SDK nicht gefunden
```powershell
dotnet --version
# Falls nicht vorhanden: https://dotnet.microsoft.com/download
```

### Autostart funktioniert nicht
- Starte PowerShell als Administrator
- Prüfe: `echo $profile` und lade ggf. Profile neu

## Quellcode verändern

Du kannst den Code direkt editieren:
- Python: `src/Dashboard/activitytracker_app.py`
- C#: `src/TraceTimeCollector/Program.cs`

Nach Änderungen erneut bauen:
```powershell
.\Build.ps1 -Clean
```

## Weitere Informationen

- [README](README.md) - Projekt-Übersicht
- Python Dashboard: `src/Dashboard/`
- C# Collector: `src/TraceTimeCollector/`
