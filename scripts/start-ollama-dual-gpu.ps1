<#
.SYNOPSIS
    Start TWO Ollama instances for DUAL GPU setup
.DESCRIPTION
    - GPU 0 (3090): PINNED models - Port 11434 (24GB - holds all 3 core models)
    - GPU 1 (5070 Ti): SWAP GPU for alternatives - Port 11435 (16GB)
#>

param(
    [string]$PrimaryModel = "deepseek-coder-v2:16b",      # ~11GB - Code generation
    [string]$ValidationModel = "phi4:latest",             # ~2.5GB - Validation (fast!)
    [string]$EmbeddingModel = "mxbai-embed-large:latest"  # ~0.7GB - Embeddings
)

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Ollama DUAL GPU Startup Script" -ForegroundColor Cyan  
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Configuration - FLIPPED: 3090 is now PINNED, 5070 Ti is SWAP
$gpu0Port = 11434  # 3090 - PINNED GPU (all core models)
$gpu1Port = 11435  # 5070 Ti - SWAP GPU (alternatives)

# Kill existing Ollama instances
Write-Host "Stopping any existing Ollama instances..." -ForegroundColor Yellow
C:\windows\system32\taskkill.exe /im ollama.exe /f 2>$null

Write-Host ""
Write-Host "GPU Configuration:" -ForegroundColor Yellow
Write-Host "  GPU 0 (3090)    → Port $gpu0Port (PINNED: deepseek + phi4 + embeddings)" -ForegroundColor Green
Write-Host "  GPU 1 (5070 Ti) → Port $gpu1Port (SWAP: alternative models)" -ForegroundColor Cyan
Write-Host ""

# Start Ollama on GPU 0 (3090) - PINNED models (never unload)
Write-Host "Starting Ollama on GPU 0 (3090) - PINNED models..." -ForegroundColor Green

$startInfo0 = New-Object System.Diagnostics.ProcessStartInfo
$startInfo0.FileName = "C:\Users\chris\AppData\Local\Programs\Ollama\ollama.exe"
$startInfo0.Arguments = "serve"
$startInfo0.UseShellExecute = $false
$startInfo0.EnvironmentVariables["CUDA_VISIBLE_DEVICES"] = "0"
$startInfo0.EnvironmentVariables["OLLAMA_KEEP_ALIVE"] = "-1"  # NEVER unload - pinned forever
$startInfo0.EnvironmentVariables["OLLAMA_HOST"] = "0.0.0.0:$gpu0Port"

$process0 = [System.Diagnostics.Process]::Start($startInfo0)
Write-Host "  Started Ollama (GPU 0 - 3090) - PID: $($process0.Id)"

# Start Ollama on GPU 1 (5070 Ti) - SWAP models (auto-unload)
Write-Host "Starting Ollama on GPU 1 (5070 Ti) - SWAP models..." -ForegroundColor Cyan

$startInfo1 = New-Object System.Diagnostics.ProcessStartInfo
$startInfo1.FileName = "C:\Users\chris\AppData\Local\Programs\Ollama\ollama.exe"
$startInfo1.Arguments = "serve"
$startInfo1.UseShellExecute = $false
$startInfo1.EnvironmentVariables["CUDA_VISIBLE_DEVICES"] = "1"
$startInfo1.EnvironmentVariables["OLLAMA_KEEP_ALIVE"] = "300"  # Unload after 5 min idle
$startInfo1.EnvironmentVariables["OLLAMA_HOST"] = "0.0.0.0:$gpu1Port"

$process1 = [System.Diagnostics.Process]::Start($startInfo1)
Write-Host "  Started Ollama (GPU 1 - 5070 Ti) - PID: $($process1.Id)"

# Wait for both servers to be ready
Write-Host ""
Write-Host "Waiting for both Ollama instances to be ready..." -ForegroundColor Yellow

function Wait-ForOllama {
    param([int]$Port, [string]$Name)
    
    $maxRetries = 30
    for ($i = 0; $i -lt $maxRetries; $i++) {
        try {
            $null = Invoke-RestMethod -Uri "http://localhost:$Port/api/tags" -TimeoutSec 5
            Write-Host "  ✅ $Name (port $Port) is ready!" -ForegroundColor Green
            return $true
        } catch {
            Start-Sleep -Seconds 2
        }
    }
    Write-Host "  ❌ $Name (port $Port) failed to start!" -ForegroundColor Red
    return $false
}

$gpu0Ready = Wait-ForOllama -Port $gpu0Port -Name "GPU 0 (3090 - PINNED)"
$gpu1Ready = Wait-ForOllama -Port $gpu1Port -Name "GPU 1 (5070 Ti - SWAP)"

if (-not $gpu0Ready -or -not $gpu1Ready) {
    Write-Host "ERROR: One or both Ollama instances failed to start!" -ForegroundColor Red
    exit 1
}

# Pre-load ALL pinned models on GPU 0 (3090)
Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "  Pre-loading PINNED models on 3090" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""

# Check available models first
Write-Host "Checking available models..." -ForegroundColor Gray
try {
    $models = (Invoke-RestMethod -Uri "http://localhost:$gpu0Port/api/tags" -TimeoutSec 10).models
    $modelNames = $models | ForEach-Object { $_.name }
    Write-Host "Available: $($modelNames -join ', ')" -ForegroundColor Gray
} catch {
    Write-Host "⚠️ Could not fetch model list" -ForegroundColor Yellow
    $modelNames = @()
}

Write-Host ""

# 1. Embedding model (~0.7GB)
Write-Host "1/3 Loading: $EmbeddingModel (~0.7GB)" -ForegroundColor Yellow
if ($modelNames -contains $EmbeddingModel -or $modelNames -contains ($EmbeddingModel -replace ':latest$', '')) {
    $body = @{ model = $EmbeddingModel; prompt = "test"; keep_alive = -1 } | ConvertTo-Json
    try {
        $null = Invoke-RestMethod -Uri "http://localhost:$gpu0Port/api/embeddings" -Method POST -Body $body -ContentType "application/json" -TimeoutSec 120
        Write-Host "    ✅ $EmbeddingModel PINNED!" -ForegroundColor Green
    } catch {
        Write-Host "    ⚠️ Could not load $EmbeddingModel - $_" -ForegroundColor Yellow
    }
} else {
    Write-Host "    ⚠️ $EmbeddingModel not installed. Run: ollama pull $EmbeddingModel" -ForegroundColor Yellow
}

# 2. Validation model - phi3.5 (~2.5GB)
Write-Host "2/3 Loading: $ValidationModel (~2.5GB)" -ForegroundColor Yellow
if ($modelNames -contains $ValidationModel -or $modelNames -contains ($ValidationModel -replace ':latest$', '')) {
    $body = @{ model = $ValidationModel; prompt = "Hi"; keep_alive = -1 } | ConvertTo-Json
    try {
        $null = Invoke-RestMethod -Uri "http://localhost:$gpu0Port/api/generate" -Method POST -Body $body -ContentType "application/json" -TimeoutSec 300
        Write-Host "    ✅ $ValidationModel PINNED!" -ForegroundColor Green
    } catch {
        Write-Host "    ⚠️ Could not load $ValidationModel - $_" -ForegroundColor Yellow
    }
} else {
    Write-Host "    ⚠️ $ValidationModel not installed. Run: ollama pull $ValidationModel" -ForegroundColor Yellow
}

# 3. Primary coding model - deepseek (~11GB)
Write-Host "3/3 Loading: $PrimaryModel (~11GB)" -ForegroundColor Yellow
if ($modelNames -contains $PrimaryModel -or $modelNames -contains ($PrimaryModel -replace ':latest$', '')) {
    $body = @{ model = $PrimaryModel; prompt = "Hi"; keep_alive = -1 } | ConvertTo-Json
    try {
        $null = Invoke-RestMethod -Uri "http://localhost:$gpu0Port/api/generate" -Method POST -Body $body -ContentType "application/json" -TimeoutSec 300
        Write-Host "    ✅ $PrimaryModel PINNED!" -ForegroundColor Green
    } catch {
        Write-Host "    ⚠️ Could not load $PrimaryModel - $_" -ForegroundColor Yellow
    }
} else {
    Write-Host "    ⚠️ $PrimaryModel not installed. Run: ollama pull $PrimaryModel" -ForegroundColor Yellow
}

# Verify all pinned models are loaded and working
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Testing PINNED Models" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$allTestsPassed = $true

# Test 1: Embedding model
Write-Host "Testing $EmbeddingModel..." -ForegroundColor Yellow
try {
    $testBody = @{ model = $EmbeddingModel; prompt = "Hello world" } | ConvertTo-Json
    $embedResult = Invoke-RestMethod -Uri "http://localhost:$gpu0Port/api/embeddings" -Method POST -Body $testBody -ContentType "application/json" -TimeoutSec 30
    if ($embedResult.embedding -and $embedResult.embedding.Count -gt 0) {
        Write-Host "  ✅ Embedding test PASSED - Generated $($embedResult.embedding.Count) dimensions" -ForegroundColor Green
    } else {
        Write-Host "  ❌ Embedding test FAILED - No embedding returned" -ForegroundColor Red
        $allTestsPassed = $false
    }
} catch {
    Write-Host "  ❌ Embedding test FAILED - $_" -ForegroundColor Red
    $allTestsPassed = $false
}

# Test 2: Validation model (phi4)
Write-Host "Testing $ValidationModel..." -ForegroundColor Yellow
try {
    $testBody = @{ model = $ValidationModel; prompt = "Say 'test passed' and nothing else."; stream = $false } | ConvertTo-Json
    $valResult = Invoke-RestMethod -Uri "http://localhost:$gpu0Port/api/generate" -Method POST -Body $testBody -ContentType "application/json" -TimeoutSec 60
    if ($valResult.response -and $valResult.response.Length -gt 0) {
        $preview = if ($valResult.response.Length -gt 50) { $valResult.response.Substring(0, 50) + "..." } else { $valResult.response }
        Write-Host "  ✅ Validation test PASSED - Response: $preview" -ForegroundColor Green
    } else {
        Write-Host "  ❌ Validation test FAILED - No response" -ForegroundColor Red
        $allTestsPassed = $false
    }
} catch {
    Write-Host "  ❌ Validation test FAILED - $_" -ForegroundColor Red
    $allTestsPassed = $false
}

# Test 3: Primary coding model (deepseek)
Write-Host "Testing $PrimaryModel..." -ForegroundColor Yellow
try {
    $testBody = @{ model = $PrimaryModel; prompt = "Write a C# hello world in one line."; stream = $false } | ConvertTo-Json
    $codeResult = Invoke-RestMethod -Uri "http://localhost:$gpu0Port/api/generate" -Method POST -Body $testBody -ContentType "application/json" -TimeoutSec 60
    if ($codeResult.response -and $codeResult.response.Length -gt 0) {
        $preview = if ($codeResult.response.Length -gt 50) { $codeResult.response.Substring(0, 50) + "..." } else { $codeResult.response }
        Write-Host "  ✅ Code gen test PASSED - Response: $preview" -ForegroundColor Green
    } else {
        Write-Host "  ❌ Code gen test FAILED - No response" -ForegroundColor Red
        $allTestsPassed = $false
    }
} catch {
    Write-Host "  ❌ Code gen test FAILED - $_" -ForegroundColor Red
    $allTestsPassed = $false
}

# Check what's actually loaded in VRAM
Write-Host ""
Write-Host "Checking loaded models on 3090..." -ForegroundColor Yellow
try {
    $psResult = Invoke-RestMethod -Uri "http://localhost:$gpu0Port/api/ps" -TimeoutSec 10
    if ($psResult.models -and $psResult.models.Count -gt 0) {
        Write-Host "  Models in VRAM:" -ForegroundColor Cyan
        foreach ($m in $psResult.models) {
            $sizeGB = [math]::Round($m.size / 1GB, 1)
            $vramGB = [math]::Round($m.size_vram / 1GB, 1)
            Write-Host "    - $($m.name): $($vramGB)GB VRAM" -ForegroundColor White
        }
        $totalVram = [math]::Round(($psResult.models | Measure-Object -Property size_vram -Sum).Sum / 1GB, 1)
        Write-Host "  Total VRAM used: $($totalVram)GB / 24GB" -ForegroundColor Cyan
    } else {
        Write-Host "  ⚠️ No models reported in VRAM (unexpected)" -ForegroundColor Yellow
    }
} catch {
    Write-Host "  ⚠️ Could not query loaded models - $_" -ForegroundColor Yellow
}

# Summary
Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "  DUAL GPU Ollama Setup Complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""

if ($allTestsPassed) {
    Write-Host "🎉 ALL TESTS PASSED - Models ready for inference!" -ForegroundColor Green
} else {
    Write-Host "⚠️ SOME TESTS FAILED - Check errors above" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "GPU 0 (3090) - Port $gpu0Port - PINNED (~14.2GB):" -ForegroundColor Green
Write-Host "  ✓ $EmbeddingModel (embeddings)"
Write-Host "  ✓ $ValidationModel (validation)"
Write-Host "  ✓ $PrimaryModel (code generation)"
Write-Host "  → Models NEVER unload (instant inference)"
Write-Host ""
Write-Host "GPU 1 (5070 Ti) - Port $gpu1Port - SWAP (16GB available):" -ForegroundColor Cyan
Write-Host "  → Ready for: qwen2.5-coder:14b, codellama:13b, etc."
Write-Host "  → Models auto-unload after 5 minutes idle"
Write-Host ""
Write-Host "API Endpoints:"
Write-Host "  Primary (3090):  http://localhost:$gpu0Port  ← Use this for all services"
Write-Host "  Swap (5070 Ti):  http://localhost:$gpu1Port  ← For alternative models"
Write-Host ""
Write-Host "Docker config:"
Write-Host "  Ollama__Url=http://10.0.0.20:$gpu0Port"
Write-Host "  Gpu__DualGpu=true"
Write-Host "  Gpu__PinnedPort=$gpu0Port"
Write-Host "  Gpu__SwapPort=$gpu1Port"
Write-Host ""
Write-Host "Press Ctrl+C to stop both instances..."

# Keep running and monitor
try {
    while ($true) {
        if ($process0.HasExited -or $process1.HasExited) {
            Write-Host "One of the Ollama instances stopped." -ForegroundColor Yellow
            break
        }
        Start-Sleep -Seconds 5
    }
} finally {
    Write-Host "Stopping Ollama instances..."
    if (-not $process0.HasExited) { $process0.Kill() }
    if (-not $process1.HasExited) { $process1.Kill() }
}
