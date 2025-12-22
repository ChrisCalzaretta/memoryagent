#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Quick test to verify extension can be triggered
#>

Write-Host ""
Write-Host "🧪 Testing Extension Manually..." -ForegroundColor Cyan
Write-Host ""

Write-Host "Testing if extension can poll for jobs..." -ForegroundColor Yellow
Write-Host ""

# Test the poller directly by calling the API
try {
    $jobs = Invoke-RestMethod -Uri "http://localhost:5001/api/orchestrator/list" -Method Get -TimeoutSec 5
    
    Write-Host "✅ Successfully polled CodingOrchestrator" -ForegroundColor Green
    Write-Host ""
    
    if ($jobs.jobs -and $jobs.jobs.Count -gt 0) {
        Write-Host "📊 Active Jobs Found: $($jobs.jobs.Count)" -ForegroundColor Cyan
        foreach ($job in $jobs.jobs) {
            Write-Host "   🔄 $($job.task) - $($job.status) ($($job.progress)%)" -ForegroundColor Gray
        }
        Write-Host ""
        Write-Host "✅ Extension should be showing these in the status bar!" -ForegroundColor Green
    } else {
        Write-Host "💤 No active jobs currently" -ForegroundColor Gray
        Write-Host ""
        Write-Host "✅ This is correct - extension should show '💤 No active jobs'" -ForegroundColor Green
    }
} catch {
    Write-Host "❌ Failed to poll: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Gray
Write-Host ""
Write-Host "🎯 What This Means:" -ForegroundColor Cyan
Write-Host ""
Write-Host "If the above test worked, the extension SHOULD work too." -ForegroundColor White
Write-Host "The extension uses the same API endpoint." -ForegroundColor Gray
Write-Host ""
Write-Host "If you still don't see the status bar after reloading:" -ForegroundColor White
Write-Host ""
Write-Host "1. Make sure you did: Ctrl+Shift+P → 'Developer: Reload Window'" -ForegroundColor Yellow
Write-Host "2. Look at the VERY BOTTOM of Cursor (not the terminal/output panel)" -ForegroundColor Yellow
Write-Host "3. Check View → Output → 'Extension Host' for errors" -ForegroundColor Yellow
Write-Host ""
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Gray
Write-Host ""


