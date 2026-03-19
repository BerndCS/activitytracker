Write-Host ">> Starte TraceTimeCollector..." -ForegroundColor Cyan
cd "src/TraceTimeCollector"
Start-Process dotnet "run" -WindowStyle Hidden

cd "../../"

Write-Host ">> Starte Dashboard..." -ForegroundColor Green
cd "src/Dashboard"
python activitytracker_app.py --minimized

Write-Host ">> Beide Dienste laufen im Hintergrund." -ForegroundColor Yellow