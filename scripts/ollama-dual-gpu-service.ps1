<#
.SYNOPSIS
    Ollama Dual GPU Service - Background service version
.DESCRIPTION
    Runs TWO Ollama instances for DUAL GPU setup as a Windows service
    - GPU 0 (3090): PINNED models - Port 11434 (24GB - holds all 3 core models)
    - GPU 1 (5070 Ti): SWAP GPU for alternatives - Port 11435 (16GB)
#>

# Service configuration
$PrimaryModel = "deepseek-coder-v2:16b"
$ValidationModel = "phi4:latest"
$EmbeddingModel = "mxbai-embed-large:latest"

$gpu0Port = 11434  # 3090 - PINNED GPU
$gpu1Port = 11435  # 5070 Ti - SWAP GPU

# Setup logging
$logPath = "C:\Logs\OllamaDualGPU"
if (-not (Test-Path $logPath)) {
    New-Item -ItemType Directory -Path $logPath -Force | Out-Null
}
$logFile = Join-Path $logPath "service-$(Get-Date -Format 'yyyy-MM-dd').log"

function Write-ServiceLog {
    param([string]$Message, [string]$Level = "INFO")
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $logMessage = "[$timestamp] [$Level] $Message"
    Add-Content -Path $logFile -Value $logMessage
    if ($Level -eq "ERROR") {
        Write-Error $logMessage
    } else {
        Write-Host $logMessage
    }
}

Write-ServiceLog "========================================" "INFO"
Write-ServiceLog "Ollama Dual GPU Service Starting" "INFO"
Write-ServiceLog "========================================" "INFO"

# Kill existing Ollama instances
Write-ServiceLog "Stopping any existing Ollama instances..." "INFO"
Get-Process -Name "ollama" -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 2

# Start Ollama on GPU 0 (3090) - PINNED models
Write-ServiceLog "Starting Ollama on GPU 0 (3090 - PINNED) on port $gpu0Port" "INFO"

$startInfo0 = New-Object System.Diagnostics.ProcessStartInfo
$startInfo0.FileName = "C:\Users\chris\AppData\Local\Programs\Ollama\ollama.exe"
$startInfo0.Arguments = "serve"
$startInfo0.UseShellExecute = $false
$startInfo0.CreateNoWindow = $true  # No console window
$startInfo0.RedirectStandardOutput = $true
$startInfo0.RedirectStandardError = $true
$startInfo0.EnvironmentVariables["CUDA_VISIBLE_DEVICES"] = "0"
$startInfo0.EnvironmentVariables["OLLAMA_KEEP_ALIVE"] = "-1"
$startInfo0.EnvironmentVariables["OLLAMA_HOST"] = "0.0.0.0:$gpu0Port"

try {
    $process0 = [System.Diagnostics.Process]::Start($startInfo0)
    Write-ServiceLog "Started Ollama GPU 0 (PID: $($process0.Id))" "INFO"
} catch {
    Write-ServiceLog "Failed to start Ollama GPU 0: $_" "ERROR"
    exit 1
}

# Start Ollama on GPU 1 (5070 Ti) - SWAP models
Write-ServiceLog "Starting Ollama on GPU 1 (5070 Ti - SWAP) on port $gpu1Port" "INFO"

$startInfo1 = New-Object System.Diagnostics.ProcessStartInfo
$startInfo1.FileName = "C:\Users\chris\AppData\Local\Programs\Ollama\ollama.exe"
$startInfo1.Arguments = "serve"
$startInfo1.UseShellExecute = $false
$startInfo1.CreateNoWindow = $true  # No console window
$startInfo1.RedirectStandardOutput = $true
$startInfo1.RedirectStandardError = $true
$startInfo1.EnvironmentVariables["CUDA_VISIBLE_DEVICES"] = "1"
$startInfo1.EnvironmentVariables["OLLAMA_KEEP_ALIVE"] = "300"
$startInfo1.EnvironmentVariables["OLLAMA_HOST"] = "0.0.0.0:$gpu1Port"

try {
    $process1 = [System.Diagnostics.Process]::Start($startInfo1)
    Write-ServiceLog "Started Ollama GPU 1 (PID: $($process1.Id))" "INFO"
} catch {
    Write-ServiceLog "Failed to start Ollama GPU 1: $_" "ERROR"
    $process0.Kill()
    exit 1
}

# Wait for both servers to be ready
Write-ServiceLog "Waiting for both Ollama instances to be ready..." "INFO"

function Wait-ForOllama {
    param([int]$Port, [string]$Name)
    
    $maxRetries = 30
    for ($i = 0; $i -lt $maxRetries; $i++) {
        try {
            $null = Invoke-RestMethod -Uri "http://localhost:$Port/api/tags" -TimeoutSec 5
            Write-ServiceLog "$Name (port $Port) is ready!" "INFO"
            return $true
        } catch {
            Start-Sleep -Seconds 2
        }
    }
    Write-ServiceLog "$Name (port $Port) failed to start!" "ERROR"
    return $false
}

$gpu0Ready = Wait-ForOllama -Port $gpu0Port -Name "GPU 0 (3090)"
$gpu1Ready = Wait-ForOllama -Port $gpu1Port -Name "GPU 1 (5070 Ti)"

if (-not $gpu0Ready -or -not $gpu1Ready) {
    Write-ServiceLog "One or both Ollama instances failed to start" "ERROR"
    exit 1
}

# Pre-load PINNED models on GPU 0
Write-ServiceLog "Pre-loading PINNED models on GPU 0 (3090)..." "INFO"

function Load-Model {
    param(
        [string]$ModelName,
        [string]$Type,  # "embeddings" or "generate"
        [int]$Port
    )
    
    try {
        if ($Type -eq "embeddings") {
            $body = @{ model = $ModelName; prompt = "test"; keep_alive = -1 } | ConvertTo-Json
            $null = Invoke-RestMethod -Uri "http://localhost:$Port/api/embeddings" -Method POST -Body $body -ContentType "application/json" -TimeoutSec 120
        } else {
            $body = @{ model = $ModelName; prompt = "Hi"; keep_alive = -1 } | ConvertTo-Json
            $null = Invoke-RestMethod -Uri "http://localhost:$Port/api/generate" -Method POST -Body $body -ContentType "application/json" -TimeoutSec 300
        }
        Write-ServiceLog "Loaded $ModelName successfully" "INFO"
        return $true
    } catch {
        Write-ServiceLog "Failed to load $ModelName : $_" "ERROR"
        return $false
    }
}

# Load all pinned models
$loadSuccess = $true
$loadSuccess = $loadSuccess -and (Load-Model -ModelName $EmbeddingModel -Type "embeddings" -Port $gpu0Port)
$loadSuccess = $loadSuccess -and (Load-Model -ModelName $ValidationModel -Type "generate" -Port $gpu0Port)
$loadSuccess = $loadSuccess -and (Load-Model -ModelName $PrimaryModel -Type "generate" -Port $gpu0Port)

if ($loadSuccess) {
    Write-ServiceLog "All PINNED models loaded successfully!" "INFO"
} else {
    Write-ServiceLog "Some models failed to load - service will continue but may have reduced functionality" "WARN"
}

Write-ServiceLog "========================================" "INFO"
Write-ServiceLog "Ollama Dual GPU Service Running" "INFO"
Write-ServiceLog "GPU 0 (3090) - Port $gpu0Port - PINNED models" "INFO"
Write-ServiceLog "GPU 1 (5070 Ti) - Port $gpu1Port - SWAP models" "INFO"
Write-ServiceLog "========================================" "INFO"

# Monitor both processes and restart if needed
$restartCount = 0
$maxRestarts = 5
$restartWindow = 300  # 5 minutes

while ($true) {
    Start-Sleep -Seconds 10
    
    # Check if processes are still running
    if ($process0.HasExited) {
        Write-ServiceLog "Ollama GPU 0 process exited unexpectedly! Exit code: $($process0.ExitCode)" "ERROR"
        
        if ($restartCount -lt $maxRestarts) {
            Write-ServiceLog "Attempting to restart GPU 0 process..." "WARN"
            try {
                $process0 = [System.Diagnostics.Process]::Start($startInfo0)
                $restartCount++
                Write-ServiceLog "Restarted GPU 0 process (PID: $($process0.Id))" "INFO"
                Start-Sleep -Seconds 30  # Give it time to stabilize
                $gpu0Ready = Wait-ForOllama -Port $gpu0Port -Name "GPU 0 (3090)"
                if ($gpu0Ready) {
                    # Reload pinned models
                    Load-Model -ModelName $EmbeddingModel -Type "embeddings" -Port $gpu0Port | Out-Null
                    Load-Model -ModelName $ValidationModel -Type "generate" -Port $gpu0Port | Out-Null
                    Load-Model -ModelName $PrimaryModel -Type "generate" -Port $gpu0Port | Out-Null
                }
            } catch {
                Write-ServiceLog "Failed to restart GPU 0: $_" "ERROR"
                break
            }
        } else {
            Write-ServiceLog "Max restart attempts reached for GPU 0" "ERROR"
            break
        }
    }
    
    if ($process1.HasExited) {
        Write-ServiceLog "Ollama GPU 1 process exited unexpectedly! Exit code: $($process1.ExitCode)" "ERROR"
        
        if ($restartCount -lt $maxRestarts) {
            Write-ServiceLog "Attempting to restart GPU 1 process..." "WARN"
            try {
                $process1 = [System.Diagnostics.Process]::Start($startInfo1)
                $restartCount++
                Write-ServiceLog "Restarted GPU 1 process (PID: $($process1.Id))" "INFO"
                Start-Sleep -Seconds 30
                Wait-ForOllama -Port $gpu1Port -Name "GPU 1 (5070 Ti)" | Out-Null
            } catch {
                Write-ServiceLog "Failed to restart GPU 1: $_" "ERROR"
                break
            }
        } else {
            Write-ServiceLog "Max restart attempts reached for GPU 1" "ERROR"
            break
        }
    }
    
    # Reset restart counter if we've been stable for the restart window
    if ($restartCount -gt 0) {
        $uptime = (Get-Date) - $process0.StartTime
        if ($uptime.TotalSeconds -gt $restartWindow) {
            $restartCount = 0
            Write-ServiceLog "Service stable - reset restart counter" "INFO"
        }
    }
}

# Cleanup on exit
Write-ServiceLog "Service stopping - cleaning up processes..." "INFO"
try {
    if (-not $process0.HasExited) { $process0.Kill(); Write-ServiceLog "Stopped GPU 0 process" "INFO" }
    if (-not $process1.HasExited) { $process1.Kill(); Write-ServiceLog "Stopped GPU 1 process" "INFO" }
} catch {
    Write-ServiceLog "Error during cleanup: $_" "ERROR"
}

Write-ServiceLog "Ollama Dual GPU Service Stopped" "INFO"
