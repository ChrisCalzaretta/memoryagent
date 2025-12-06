<#
.SYNOPSIS
    Stop all running Ollama instances
#>

Write-Host "Stopping all Ollama instances..." -ForegroundColor Yellow

$processes = Get-Process -Name "ollama" -ErrorAction SilentlyContinue

if ($processes) {
    foreach ($proc in $processes) {
        Write-Host "  Stopping Ollama PID: $($proc.Id)"
        Stop-Process -Id $proc.Id -Force
    }
    Write-Host "✅ All Ollama instances stopped." -ForegroundColor Green
} else {
    Write-Host "No Ollama instances running." -ForegroundColor Yellow
}


