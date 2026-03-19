param(
    [Parameter(Mandatory = $true)]
    [string]$RepoOwner,

    [Parameter(Mandatory = $true)]
    [string]$RepoName,

    [string]$AssetName = "activitytracker_bundle.zip",
    [switch]$AllowStableFallback = $true
)

$ErrorActionPreference = "Stop"

function Stop-ActivityProcesses {
    Write-Host "[1/6] Stoppe laufende Prozesse..." -ForegroundColor Yellow

    $processNames = @("activitytracker_app", "TraceTimeCollector")
    foreach ($name in $processNames) {
        Get-Process -Name $name -ErrorAction SilentlyContinue | ForEach-Object {
            try {
                Stop-Process -Id $_.Id -Force
                Write-Host "  ✓ Gestoppt: $($_.ProcessName) (PID $($_.Id))" -ForegroundColor Green
            }
            catch {
                Write-Host "  ! Konnte Prozess nicht beenden: $($_.ProcessName)" -ForegroundColor DarkYellow
            }
        }
    }
}

function Get-ReleaseAssetUrl {
    param(
        [string]$Owner,
        [string]$Repo,
        [string]$ExpectedAsset,
        [bool]$StableFallback
    )

    $releasesUrl = "https://api.github.com/repos/$Owner/$Repo/releases"
    $headers = @{
        "User-Agent" = "ActivityTracker-Updater"
        "Accept" = "application/vnd.github+json"
    }

    Write-Host "[2/6] Lade Release-Liste von GitHub..." -ForegroundColor Yellow
    $releases = Invoke-RestMethod -Uri $releasesUrl -Headers $headers
    if (-not $releases) {
        throw "Keine Releases gefunden."
    }

    $candidates = $releases | Where-Object {
        $_.prerelease -eq $true -and $_.tag_name -match "(?i)rc"
    } | Sort-Object -Property published_at -Descending

    $selectedRelease = $candidates | Select-Object -First 1
    if (-not $selectedRelease -and $StableFallback) {
        $selectedRelease = $releases | Where-Object { $_.prerelease -eq $false } | Sort-Object -Property published_at -Descending | Select-Object -First 1
    }

    if (-not $selectedRelease) {
        throw "Kein passender Release Candidate gefunden."
    }

    $asset = $selectedRelease.assets | Where-Object { $_.name -eq $ExpectedAsset } | Select-Object -First 1
    if (-not $asset) {
        throw "Asset '$ExpectedAsset' wurde in Release '$($selectedRelease.tag_name)' nicht gefunden."
    }

    return @{
        Tag = $selectedRelease.tag_name
        Url = $asset.browser_download_url
        AssetName = $asset.name
    }
}

function Copy-NewBinaries {
    param(
        [string]$InstallDir,
        [string]$ExtractDir
    )

    Write-Host "[4/6] Ersetze EXE-Dateien..." -ForegroundColor Yellow

    $dashboardExe = Get-ChildItem -Path $ExtractDir -Recurse -Filter "activitytracker_app.exe" | Select-Object -First 1
    $collectorExe = Get-ChildItem -Path $ExtractDir -Recurse -Filter "TraceTimeCollector.exe" | Select-Object -First 1

    if (-not $dashboardExe -or -not $collectorExe) {
        throw "Im Bundle fehlen erforderliche Dateien (activitytracker_app.exe / TraceTimeCollector.exe)."
    }

    $timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
    $backupDir = Join-Path $InstallDir "backup\$timestamp"
    New-Item -ItemType Directory -Path $backupDir -Force | Out-Null

    foreach ($exeName in @("activitytracker_app.exe", "TraceTimeCollector.exe")) {
        $currentPath = Join-Path $InstallDir $exeName
        if (Test-Path $currentPath) {
            Copy-Item $currentPath $backupDir -Force
        }
    }

    Copy-Item $dashboardExe.FullName (Join-Path $InstallDir "activitytracker_app.exe") -Force
    Copy-Item $collectorExe.FullName (Join-Path $InstallDir "TraceTimeCollector.exe") -Force

    Write-Host "  ✓ EXEs aktualisiert" -ForegroundColor Green
    Write-Host "  ✓ Backup unter: $backupDir" -ForegroundColor Green
}

function Start-UpdatedProcesses {
    param([string]$InstallDir)

    Write-Host "[5/6] Starte aktualisierte Prozesse..." -ForegroundColor Yellow

    $collectorPath = Join-Path $InstallDir "TraceTimeCollector.exe"
    $dashboardPath = Join-Path $InstallDir "activitytracker_app.exe"

    if (-not (Test-Path $collectorPath) -or -not (Test-Path $dashboardPath)) {
        throw "Aktualisierte EXE-Dateien wurden nicht gefunden."
    }

    Start-Process -FilePath $collectorPath -WindowStyle Hidden
    Start-Process -FilePath $dashboardPath -ArgumentList "--minimized"

    Write-Host "  ✓ TraceTimeCollector gestartet" -ForegroundColor Green
    Write-Host "  ✓ Dashboard minimiert gestartet" -ForegroundColor Green
}

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$installDir = Join-Path $scriptRoot "dist"
if (-not (Test-Path $installDir)) {
    $installDir = $scriptRoot
}

$tempRoot = Join-Path $env:TEMP "ActivityTrackerUpdate"
if (Test-Path $tempRoot) {
    Remove-Item $tempRoot -Recurse -Force
}
New-Item -ItemType Directory -Path $tempRoot -Force | Out-Null

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "ActivityTracker Updater" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Installationsordner: $installDir" -ForegroundColor White
Write-Host "Hinweis: DB bleibt erhalten unter %APPDATA%\TraceTime\activity_log.db" -ForegroundColor White

Stop-ActivityProcesses

$releaseInfo = Get-ReleaseAssetUrl -Owner $RepoOwner -Repo $RepoName -ExpectedAsset $AssetName -StableFallback $AllowStableFallback
Write-Host "  ✓ Gewähltes Release: $($releaseInfo.Tag)" -ForegroundColor Green

Write-Host "[3/6] Lade Update-Bundle herunter..." -ForegroundColor Yellow
$zipPath = Join-Path $tempRoot $releaseInfo.AssetName
Invoke-WebRequest -Uri $releaseInfo.Url -OutFile $zipPath
Write-Host "  ✓ Download abgeschlossen: $zipPath" -ForegroundColor Green

$extractDir = Join-Path $tempRoot "extract"
Expand-Archive -Path $zipPath -DestinationPath $extractDir -Force

Copy-NewBinaries -InstallDir $installDir -ExtractDir $extractDir
Start-UpdatedProcesses -InstallDir $installDir

Write-Host "[6/6] Update abgeschlossen" -ForegroundColor Green
Write-Host "Nahtloses Update erfolgreich. Bestehende Datenbank wurde nicht verändert." -ForegroundColor Green
