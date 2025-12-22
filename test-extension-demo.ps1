#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Test the Job Status Extension

.DESCRIPTION
    Starts a simple code generation job to test the extension
#>

Write-Host "🧪 Testing Job Status Extension..." -ForegroundColor Cyan
Write-Host ""
Write-Host "1️⃣  Reload Cursor window (Ctrl+Shift+P → 'Developer: Reload Window')" -ForegroundColor Yellow
Write-Host "2️⃣  Look at bottom-left corner - should show '💤 No active jobs'" -ForegroundColor Yellow
Write-Host "3️⃣  Press any key when ready to start test job..." -ForegroundColor Yellow
$null = $Host.UI.RawUI.ReadKey('NoEcho,IncludeKeyDown')

Write-Host ""
Write-Host "🚀 Starting test job..." -ForegroundColor Cyan

# Start a simple job via MCP
$body = @{
    task = "Create a simple Calculator class with Add and Subtract methods"
    language = "csharp"
    maxIterations = 5
    background = $true
} | ConvertTo-Json

$response = Invoke-RestMethod -Uri "http://localhost:5001/api/orchestrator/orchestrate" -Method Post -Body $body -ContentType "application/json"

Write-Host ""
Write-Host "✅ Job started!" -ForegroundColor Green
Write-Host "   Job ID: $($response.jobId)" -ForegroundColor Gray
Write-Host ""
Write-Host "👀 Watch the status bar - it should update to:" -ForegroundColor Cyan
Write-Host "   🔄 Create a simple Calculator... (0%) | ⏱️ 2s" -ForegroundColor Yellow
Write-Host ""
Write-Host "   Then it will update every 3 seconds with progress!" -ForegroundColor Gray
Write-Host ""
Write-Host "💡 Click the status bar to see detailed job view" -ForegroundColor Cyan
Write-Host ""
Write-Host "🎯 Test Features:" -ForegroundColor Cyan
Write-Host "   - Status bar updates automatically" -ForegroundColor Gray
Write-Host "   - Click for detailed view" -ForegroundColor Gray
Write-Host "   - Desktop notification when complete" -ForegroundColor Gray
Write-Host "   - Hover over status bar for tooltip" -ForegroundColor Gray
Write-Host ""



