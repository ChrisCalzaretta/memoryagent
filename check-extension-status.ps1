#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Check if Job Status Extension is working
#>

Write-Host ""
Write-Host "🔍 Checking Extension Status..." -ForegroundColor Cyan
Write-Host ""

# Check installation
$extPath = "$env:USERPROFILE\.cursor\extensions\cursor-job-status-1.0.0"
Write-Host "1. Extension Files:" -ForegroundColor Yellow
if (Test-Path $extPath) {
    Write-Host "   ✅ Extension is installed at: $extPath" -ForegroundColor Green
    $files = Get-ChildItem $extPath -File
    Write-Host "   Files found:" -ForegroundColor Gray
    foreach ($f in $files) {
        Write-Host "      - $($f.Name)" -ForegroundColor Gray
    }
} else {
    Write-Host "   ❌ Extension NOT found!" -ForegroundColor Red
    Write-Host "   Run: .\install-job-status-extension.ps1" -ForegroundColor Yellow
    exit 1
}

Write-Host ""
Write-Host "2. Services Status:" -ForegroundColor Yellow

# Check CodingOrchestrator
try {
    $response = Invoke-RestMethod -Uri "http://localhost:5001/health" -TimeoutSec 2 -ErrorAction Stop
    Write-Host "   ✅ CodingOrchestrator: Running" -ForegroundColor Green
} catch {
    Write-Host "   ❌ CodingOrchestrator: NOT running" -ForegroundColor Red
    Write-Host "      The extension won't show jobs if the service is down" -ForegroundColor Yellow
}

# Check MemoryRouter
try {
    $response = Invoke-RestMethod -Uri "http://localhost:5010/health" -TimeoutSec 2 -ErrorAction Stop
    Write-Host "   ✅ MemoryRouter: Running" -ForegroundColor Green
} catch {
    Write-Host "   ⚠️  MemoryRouter: NOT running (optional)" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Gray
Write-Host ""
Write-Host "📋 NEXT STEPS:" -ForegroundColor Cyan
Write-Host ""
Write-Host "If you haven't reloaded Cursor yet:" -ForegroundColor White
Write-Host "   1. Press: Ctrl + Shift + P" -ForegroundColor Gray
Write-Host "   2. Type: reload" -ForegroundColor Gray
Write-Host "   3. Select: Developer: Reload Window" -ForegroundColor Gray
Write-Host ""
Write-Host "After reload, look at BOTTOM-LEFT corner:" -ForegroundColor White
Write-Host "   You should see: 💤 No active jobs" -ForegroundColor Gray
Write-Host ""
Write-Host "If you still don't see it:" -ForegroundColor White
Write-Host "   1. Open: View → Output" -ForegroundColor Gray
Write-Host "   2. Dropdown: Select 'Extension Host'" -ForegroundColor Gray
Write-Host "   3. Look for errors about 'cursor-job-status'" -ForegroundColor Gray
Write-Host ""
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Gray
Write-Host ""



