<#
.SYNOPSIS
    Start Ollama for SINGLE GPU setup with pinned models
.DESCRIPTION
    - Starts Ollama with keep_alive=-1 (models stay loaded)
    - Pre-loads the pinned models (deepseek + embedding)
    - Single GPU mode
#>

param(
    [int]$Port = 11434,
    [int]$GpuId = 0,
    [string]$PrimaryModel = "deepseek-v2:16b",
    [string]$EmbeddingModel = "mxbai-embed-large:latest"
)

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Ollama Single GPU Startup Script" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "GPU ID: $GpuId"
Write-Host "Port: $Port"
Write-Host "Primary Model: $PrimaryModel"
Write-Host "Embedding Model: $EmbeddingModel"
Write-Host ""

# Set environment variables for this session
$env:CUDA_VISIBLE_DEVICES = "$GpuId"
$env:OLLAMA_KEEP_ALIVE = "-1"  # Never unload models
$env:OLLAMA_HOST = "0.0.0.0:$Port"

Write-Host "Environment configured:" -ForegroundColor Yellow
Write-Host "  CUDA_VISIBLE_DEVICES = $env:CUDA_VISIBLE_DEVICES"
Write-Host "  OLLAMA_KEEP_ALIVE = $env:OLLAMA_KEEP_ALIVE"
Write-Host "  OLLAMA_HOST = $env:OLLAMA_HOST"
Write-Host ""

# Start Ollama server in background
Write-Host "Starting Ollama server..." -ForegroundColor Green
$ollamaProcess = Start-Process -FilePath "ollama" -ArgumentList "serve" -PassThru -NoNewWindow

# Wait for server to be ready
Write-Host "Waiting for Ollama to be ready..." -ForegroundColor Yellow
$maxRetries = 30
$retryCount = 0
do {
    Start-Sleep -Seconds 2
    $retryCount++
    try {
        $response = Invoke-RestMethod -Uri "http://localhost:$Port/api/tags" -TimeoutSec 5
        $ready = $true
        Write-Host "Ollama is ready!" -ForegroundColor Green
    } catch {
        $ready = $false
        Write-Host "  Waiting... ($retryCount/$maxRetries)"
    }
} while (-not $ready -and $retryCount -lt $maxRetries)

if (-not $ready) {
    Write-Host "ERROR: Ollama failed to start!" -ForegroundColor Red
    exit 1
}

# Pre-load pinned models
Write-Host ""
Write-Host "Pre-loading PINNED models (these will NEVER be unloaded)..." -ForegroundColor Cyan

# Load embedding model first (small, fast)
Write-Host "  Loading embedding model: $EmbeddingModel" -ForegroundColor Yellow
$body = @{
    model = $EmbeddingModel
    keep_alive = -1
} | ConvertTo-Json

try {
    Invoke-RestMethod -Uri "http://localhost:$Port/api/generate" -Method POST -Body $body -ContentType "application/json" -TimeoutSec 120
    Write-Host "  ✅ $EmbeddingModel loaded and pinned!" -ForegroundColor Green
} catch {
    Write-Host "  ⚠️ Warning: Could not pre-load $EmbeddingModel" -ForegroundColor Yellow
}

# Load primary model
Write-Host "  Loading primary model: $PrimaryModel" -ForegroundColor Yellow
$body = @{
    model = $PrimaryModel
    prompt = "Hello"  # Minimal prompt to trigger load
    keep_alive = -1
} | ConvertTo-Json

try {
    Invoke-RestMethod -Uri "http://localhost:$Port/api/generate" -Method POST -Body $body -ContentType "application/json" -TimeoutSec 300
    Write-Host "  ✅ $PrimaryModel loaded and pinned!" -ForegroundColor Green
} catch {
    Write-Host "  ⚠️ Warning: Could not pre-load $PrimaryModel" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "  Ollama ready with pinned models!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "Pinned models (NEVER unloaded):"
Write-Host "  - $EmbeddingModel"
Write-Host "  - $PrimaryModel"
Write-Host ""
Write-Host "API endpoint: http://localhost:$Port"
Write-Host ""
Write-Host "Press Ctrl+C to stop Ollama..."

# Keep script running
try {
    Wait-Process -Id $ollamaProcess.Id
} catch {
    Write-Host "Ollama stopped." -ForegroundColor Yellow
}


