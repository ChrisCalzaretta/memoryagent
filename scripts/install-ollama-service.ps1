<#
.SYNOPSIS
    Install Ollama Dual GPU as a Windows Service
.DESCRIPTION
    Uses NSSM (Non-Sucking Service Manager) to install the Ollama service
    If NSSM is not installed, it will be downloaded automatically
#>

#Requires -RunAsAdministrator

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Ollama Dual GPU Service Installer" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$serviceName = "OllamaDualGPU"
$scriptPath = Join-Path $PSScriptRoot "ollama-dual-gpu-service.ps1"
$nssmPath = "C:\Tools\nssm\nssm.exe"
$nssmDownloadUrl = "https://nssm.cc/release/nssm-2.24.zip"

# Check if service already exists
$existingService = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
if ($existingService) {
    Write-Host "Service '$serviceName' already exists." -ForegroundColor Yellow
    $response = Read-Host "Do you want to reinstall? (y/n)"
    if ($response -ne 'y') {
        Write-Host "Installation cancelled." -ForegroundColor Yellow
        exit 0
    }
    
    Write-Host "Stopping and removing existing service..." -ForegroundColor Yellow
    Stop-Service -Name $serviceName -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2
    
    if (Test-Path $nssmPath) {
        & $nssmPath remove $serviceName confirm
    } else {
        sc.exe delete $serviceName
    }
    Start-Sleep -Seconds 2
}

# Check if NSSM is installed
if (-not (Test-Path $nssmPath)) {
    Write-Host "NSSM not found at $nssmPath" -ForegroundColor Yellow
    Write-Host "Downloading NSSM..." -ForegroundColor Cyan
    
    $tempDir = Join-Path $env:TEMP "nssm-download"
    $zipPath = Join-Path $tempDir "nssm.zip"
    
    if (-not (Test-Path $tempDir)) {
        New-Item -ItemType Directory -Path $tempDir | Out-Null
    }
    
    try {
        Invoke-WebRequest -Uri $nssmDownloadUrl -OutFile $zipPath
        Write-Host "Extracting NSSM..." -ForegroundColor Cyan
        Expand-Archive -Path $zipPath -DestinationPath $tempDir -Force
        
        # Find nssm.exe in extracted files
        $nssmExe = Get-ChildItem -Path $tempDir -Filter "nssm.exe" -Recurse | Where-Object { $_.FullName -like "*win64*" } | Select-Object -First 1
        
        if ($nssmExe) {
            $nssmInstallDir = "C:\Tools\nssm"
            if (-not (Test-Path $nssmInstallDir)) {
                New-Item -ItemType Directory -Path $nssmInstallDir -Force | Out-Null
            }
            Copy-Item -Path $nssmExe.FullName -Destination $nssmPath -Force
            Write-Host "NSSM installed to $nssmPath" -ForegroundColor Green
        } else {
            throw "Could not find nssm.exe in downloaded archive"
        }
    } catch {
        Write-Host "Failed to download/install NSSM: $_" -ForegroundColor Red
        Write-Host ""
        Write-Host "Please download NSSM manually from: https://nssm.cc/download" -ForegroundColor Yellow
        Write-Host "Extract it to: C:\Tools\nssm\nssm.exe" -ForegroundColor Yellow
        Write-Host "Then run this script again." -ForegroundColor Yellow
        exit 1
    } finally {
        if (Test-Path $tempDir) {
            Remove-Item -Path $tempDir -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
}

# Verify script exists
if (-not (Test-Path $scriptPath)) {
    Write-Host "ERROR: Service script not found at: $scriptPath" -ForegroundColor Red
    exit 1
}

# Create log directory
$logDir = "C:\Logs\OllamaDualGPU"
if (-not (Test-Path $logDir)) {
    New-Item -ItemType Directory -Path $logDir -Force | Out-Null
    Write-Host "Created log directory: $logDir" -ForegroundColor Green
}

# Install service using NSSM
Write-Host ""
Write-Host "Installing service..." -ForegroundColor Cyan

$pwshPath = (Get-Command pwsh -ErrorAction SilentlyContinue).Source
if (-not $pwshPath) {
    $pwshPath = (Get-Command powershell).Source
    Write-Host "Using Windows PowerShell: $pwshPath" -ForegroundColor Yellow
} else {
    Write-Host "Using PowerShell Core: $pwshPath" -ForegroundColor Green
}

# Install service
& $nssmPath install $serviceName $pwshPath "-ExecutionPolicy Bypass -NoProfile -File `"$scriptPath`""

# Configure service
Write-Host "Configuring service..." -ForegroundColor Cyan
& $nssmPath set $serviceName DisplayName "Ollama Dual GPU Service"
& $nssmPath set $serviceName Description "Runs two Ollama instances on separate GPUs for optimal AI inference performance"
& $nssmPath set $serviceName Start SERVICE_AUTO_START  # Start automatically with Windows
& $nssmPath set $serviceName AppStdout "$logDir\stdout.log"
& $nssmPath set $serviceName AppStderr "$logDir\stderr.log"
& $nssmPath set $serviceName AppRotateFiles 1
& $nssmPath set $serviceName AppRotateBytes 10485760  # 10MB log rotation
& $nssmPath set $serviceName ObjectName "LocalSystem"  # Run as SYSTEM account

# Set restart policy
& $nssmPath set $serviceName AppThrottle 10000  # Wait 10 seconds before restart
& $nssmPath set $serviceName AppExit Default Restart
& $nssmPath set $serviceName AppRestartDelay 5000  # 5 second delay between restarts

Write-Host ""
Write-Host "Service installed successfully!" -ForegroundColor Green
Write-Host ""
Write-Host "Service Details:" -ForegroundColor Cyan
Write-Host "  Name:        $serviceName" -ForegroundColor White
Write-Host "  Display:     Ollama Dual GPU Service" -ForegroundColor White
Write-Host "  Status:      Installed (not started)" -ForegroundColor White
Write-Host "  Startup:     Automatic (starts with Windows)" -ForegroundColor White
Write-Host "  Logs:        $logDir" -ForegroundColor White
Write-Host ""

$response = Read-Host "Do you want to start the service now? (y/n)"
if ($response -eq 'y') {
    Write-Host ""
    Write-Host "Starting service..." -ForegroundColor Cyan
    Start-Service -Name $serviceName
    Start-Sleep -Seconds 5
    
    $service = Get-Service -Name $serviceName
    if ($service.Status -eq 'Running') {
        Write-Host "Service started successfully!" -ForegroundColor Green
        Write-Host ""
        Write-Host "Ollama endpoints:" -ForegroundColor Cyan
        Write-Host "  GPU 0 (3090 - PINNED):   http://localhost:11434" -ForegroundColor Green
        Write-Host "  GPU 1 (5070 Ti - SWAP):  http://localhost:11435" -ForegroundColor Cyan
        Write-Host ""
        Write-Host "Checking service health..." -ForegroundColor Yellow
        Start-Sleep -Seconds 15
        
        try {
            $tags = Invoke-RestMethod -Uri "http://localhost:11434/api/tags" -TimeoutSec 10
            Write-Host "✅ GPU 0 (port 11434) is responding - $($tags.models.Count) models available" -ForegroundColor Green
        } catch {
            Write-Host "⚠️  GPU 0 not responding yet - check logs in $logDir" -ForegroundColor Yellow
        }
        
        try {
            $tags = Invoke-RestMethod -Uri "http://localhost:11435/api/tags" -TimeoutSec 10
            Write-Host "✅ GPU 1 (port 11435) is responding - $($tags.models.Count) models available" -ForegroundColor Green
        } catch {
            Write-Host "⚠️  GPU 1 not responding yet - check logs in $logDir" -ForegroundColor Yellow
        }
    } else {
        Write-Host "Service failed to start. Status: $($service.Status)" -ForegroundColor Red
        Write-Host "Check logs at: $logDir" -ForegroundColor Yellow
    }
} else {
    Write-Host ""
    Write-Host "Service installed but not started." -ForegroundColor Yellow
    Write-Host "To start manually: Start-Service -Name $serviceName" -ForegroundColor White
}

Write-Host ""
Write-Host "Management Commands:" -ForegroundColor Cyan
Write-Host "  Start:     Start-Service -Name $serviceName" -ForegroundColor White
Write-Host "  Stop:      Stop-Service -Name $serviceName" -ForegroundColor White
Write-Host "  Status:    Get-Service -Name $serviceName" -ForegroundColor White
Write-Host "  Logs:      Get-Content $logDir\service-*.log -Tail 50" -ForegroundColor White
Write-Host "  Uninstall: Run uninstall-ollama-service.ps1" -ForegroundColor White
Write-Host ""
