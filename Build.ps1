
param(
    [switch]$Clean = $false,
    [switch]$CreateReleaseBundle = $false,
    [string]$BundleName = "activitytracker_bundle.zip"
)

$ProjectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$SrcDir = Join-Path $ProjectRoot "src"
$DistDir = Join-Path $ProjectRoot "dist"
$BuildDir = Join-Path $ProjectRoot "build"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "ActivityTracker Build-Script" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

if ($Clean) {
    Write-Host "`n[1/5] Räume auf..." -ForegroundColor Yellow
    if (Test-Path $DistDir) { Remove-Item $DistDir -Recurse -Force }
    if (Test-Path $BuildDir) { Remove-Item $BuildDir -Recurse -Force }
    Write-Host "✓ Aufgeräumt" -ForegroundColor Green
}

New-Item -ItemType Directory -Path $DistDir -Force | Out-Null

Write-Host "`n[2/5] Installiere Python-Dependencies..." -ForegroundColor Yellow
$RequirementsFile = Join-Path $ProjectRoot "requirements.txt"
if (Test-Path $RequirementsFile) {
    python -m pip install --upgrade pip -q
    pip install -r $RequirementsFile -q
    Write-Host "✓ Dependencies installiert" -ForegroundColor Green
} else {
    Write-Host "⚠ requirements.txt nicht gefunden!" -ForegroundColor Red
}

Write-Host "`n[3/5] Installiere PyInstaller..." -ForegroundColor Yellow
pip install pyinstaller -q
Write-Host "✓ PyInstaller installiert" -ForegroundColor Green

Write-Host "`n[4/5] Kompiliere Dashboard (Python → .exe)..." -ForegroundColor Yellow
$DashboardPy = Join-Path $SrcDir "Dashboard" "activitytracker_app.py"
if (Test-Path $DashboardPy) {
    $BuildTempDir = Join-Path $BuildDir "dashboard_build"
    pyinstaller `
        --onefile `
        --windowed `
        --name "activitytracker_app" `
        --distpath $DistDir `
        --buildpath $BuildTempDir `
        --specpath $BuildTempDir `
        --add-data "$([IO.Path]::GetDirectoryName($DashboardPy));." `
        $DashboardPy | Out-Null
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✓ Dashboard .exe erstellt" -ForegroundColor Green
    } else {
        Write-Host "✗ Fehler beim PyInstaller-Build!" -ForegroundColor Red
        exit 1
    }
} else {
    Write-Host "✗ $DashboardPy nicht gefunden!" -ForegroundColor Red
    exit 1
}

Write-Host "`n[5/5] Kompiliere Collector (C# → .exe)..." -ForegroundColor Yellow
$CsprojFile = Join-Path $SrcDir "TraceTimeCollector" "TraceTimeCollector.csproj"
if (Test-Path $CsprojFile) {
    Push-Location $SrcDir\TraceTimeCollector
    dotnet build -c Release -o $DistDir 2>&1 | Out-Null
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✓ TraceTimeCollector .exe erstellt" -ForegroundColor Green
    } else {
        Write-Host "✗ Fehler beim dotnet build!" -ForegroundColor Red
        Pop-Location
        exit 1
    }
    Pop-Location
} else {
    Write-Host "✗ $CsprojFile nicht gefunden!" -ForegroundColor Red
    exit 1
}

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "✓ Build erfolgreich abgeschlossen!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "`nDateien im dist-Ordner:" -ForegroundColor White
Get-ChildItem $DistDir -Filter "*.exe" | ForEach-Object {
    Write-Host "  ✓ $($_.Name)" -ForegroundColor Cyan
}

Write-Host "`n📌 Nächste Schritte:" -ForegroundColor Yellow
Write-Host "  1. Teste beide .exe-Dateien im dist-Ordner" -ForegroundColor White
Write-Host "  2. Führe das Autostart-Skript aus: .\Create-AutostartShortcut.ps1" -ForegroundColor White
Write-Host "  3. Oder erstelle einen Installer mit NSIS/WiX" -ForegroundColor White

if ($CreateReleaseBundle) {
    Write-Host "`n[Optional] Erstelle Release-Bundle..." -ForegroundColor Yellow
    $BundlePath = Join-Path $DistDir $BundleName
    $StageDir = Join-Path $BuildDir "release_bundle"

    if (Test-Path $StageDir) { Remove-Item $StageDir -Recurse -Force }
    New-Item -ItemType Directory -Path $StageDir -Force | Out-Null

    Copy-Item (Join-Path $DistDir "activitytracker_app.exe") $StageDir -Force
    Copy-Item (Join-Path $DistDir "TraceTimeCollector.exe") $StageDir -Force

    if (Test-Path $BundlePath) {
        Remove-Item $BundlePath -Force
    }

    Compress-Archive -Path (Join-Path $StageDir "*") -DestinationPath $BundlePath -Force
    Write-Host "✓ Release-Bundle erstellt: $BundlePath" -ForegroundColor Green
}
