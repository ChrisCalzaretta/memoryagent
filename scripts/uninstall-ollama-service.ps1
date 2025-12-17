<#
.SYNOPSIS
    Uninstall Ollama Dual GPU Windows Service
.DESCRIPTION
    Stops and removes the Ollama Dual GPU service from Windows
#>

#Requires -RunAsAdministrator

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Ollama Dual GPU Service Uninstaller" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$serviceName = "OllamaDualGPU"
$nssmPath = "C:\Tools\nssm\nssm.exe"

# Check if service exists
$service = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
if (-not $service) {
    Write-Host "Service '$serviceName' is not installed." -ForegroundColor Yellow
    exit 0
}

Write-Host "Current service status: $($service.Status)" -ForegroundColor Cyan
Write-Host ""
$response = Read-Host "Are you sure you want to uninstall the service? (y/n)"
if ($response -ne 'y') {
    Write-Host "Uninstall cancelled." -ForegroundColor Yellow
    exit 0
}

# Stop service if running
Write-Host ""
if ($service.Status -eq 'Running') {
    Write-Host "Stopping service..." -ForegroundColor Yellow
    Stop-Service -Name $serviceName -Force
    Start-Sleep -Seconds 5
    
    # Force kill any remaining Ollama processes
    Get-Process -Name "ollama" -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep -Seconds 2
}

# Remove service
Write-Host "Removing service..." -ForegroundColor Yellow
if (Test-Path $nssmPath) {
    & $nssmPath remove $serviceName confirm
} else {
    sc.exe delete $serviceName
}

Start-Sleep -Seconds 2

# Verify removal
$service = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
if ($service) {
    Write-Host "Failed to remove service." -ForegroundColor Red
    exit 1
} else {
    Write-Host ""
    Write-Host "Service uninstalled successfully!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Note: Log files remain at C:\Logs\OllamaDualGPU" -ForegroundColor Cyan
    Write-Host "      NSSM remains at $nssmPath" -ForegroundColor Cyan
    Write-Host ""
    $cleanup = Read-Host "Delete log files and NSSM? (y/n)"
    if ($cleanup -eq 'y') {
        if (Test-Path "C:\Logs\OllamaDualGPU") {
            Remove-Item -Path "C:\Logs\OllamaDualGPU" -Recurse -Force
            Write-Host "Deleted log files" -ForegroundColor Green
        }
        if (Test-Path "C:\Tools\nssm") {
            Remove-Item -Path "C:\Tools\nssm" -Recurse -Force
            Write-Host "Deleted NSSM" -ForegroundColor Green
        }
    }
}

Write-Host ""
Write-Host "Uninstall complete!" -ForegroundColor Green
Write-Host ""
