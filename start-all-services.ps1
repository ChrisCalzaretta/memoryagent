# Start all required services for MemoryRouter
Write-Host "🚀 Starting MemoryAgent services..." -ForegroundColor Cyan
Write-Host ""

# Kill any existing instances
Write-Host "🧹 Cleaning up old instances..." -ForegroundColor Yellow
Get-Process -Name "dotnet" -ErrorAction SilentlyContinue | Where-Object { 
    $_.MainWindowTitle -match "MemoryAgent|CodingOrchestrator|MemoryRouter" 
} | Stop-Process -Force -ErrorAction SilentlyContinue

Start-Sleep -Seconds 2

# Start MemoryAgent (port 5000)
Write-Host "1️⃣  Starting MemoryAgent on port 5000..." -ForegroundColor Green
Start-Process powershell -ArgumentList @(
    "-NoExit",
    "-Command",
    "cd E:\GitHub\MemoryAgent\MemoryAgent.Server; `$env:ASPNETCORE_ENVIRONMENT='Development'; dotnet run --urls http://localhost:5000"
) -WindowStyle Normal

Start-Sleep -Seconds 3

# Start CodingOrchestrator (port 5003)
Write-Host "2️⃣  Starting CodingOrchestrator on port 5003..." -ForegroundColor Green
Start-Process powershell -ArgumentList @(
    "-NoExit",
    "-Command",
    "cd E:\GitHub\MemoryAgent\CodingOrchestrator.Server; `$env:ASPNETCORE_ENVIRONMENT='Development'; dotnet run --urls http://localhost:5003"
) -WindowStyle Normal

Start-Sleep -Seconds 3

# Start MemoryRouter (port 5010)
Write-Host "3️⃣  Starting MemoryRouter on port 5010..." -ForegroundColor Green
Start-Process powershell -ArgumentList @(
    "-NoExit",
    "-Command",
    "cd E:\GitHub\MemoryAgent\MemoryRouter.Server; `$env:ASPNETCORE_ENVIRONMENT='Development'; dotnet run --urls http://localhost:5010"
) -WindowStyle Normal

Write-Host ""
Write-Host "⏳ Waiting for services to start..." -ForegroundColor Yellow
Start-Sleep -Seconds 10

Write-Host ""
Write-Host "✅ All services should be starting!" -ForegroundColor Green
Write-Host ""
Write-Host "📋 Service URLs:" -ForegroundColor Cyan
Write-Host "   MemoryAgent:         http://localhost:5000" -ForegroundColor White
Write-Host "   CodingOrchestrator:  http://localhost:5003" -ForegroundColor White
Write-Host "   MemoryRouter:        http://localhost:5010" -ForegroundColor White
Write-Host "   Ollama:              http://localhost:11434 (already running)" -ForegroundColor White
Write-Host ""
Write-Host "🧪 Test MemoryRouter:" -ForegroundColor Cyan
Write-Host "   Invoke-RestMethod http://localhost:5010/health" -ForegroundColor Gray
Write-Host ""
Write-Host "⚠️  Note: Watch each service window for startup logs!" -ForegroundColor Yellow
