# Memory Agent - Stop Shared Stack

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Memory Agent - Stop Shared Stack" -ForegroundColor Yellow
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

Write-Host "Stopping shared MCP stack..." -ForegroundColor Yellow

docker-compose -f docker-compose-shared.yml down

if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Failed to stop shared stack" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "✅ Shared stack stopped" -ForegroundColor Green
Write-Host ""
Write-Host "Note: Data is preserved in d:\Memory\shared\" -ForegroundColor Yellow
Write-Host ""

