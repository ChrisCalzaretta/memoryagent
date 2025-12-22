# Test Model Context Loading
# Run this to manually load models with specific context sizes

Write-Host "`n=== TESTING MODEL CONTEXT LOADING ===" -ForegroundColor Cyan

# 1. Test Qwen at 128k
Write-Host "`n[1] Testing Qwen 2.5 Coder at 128k context..." -ForegroundColor Yellow
ollama stop qwen2.5-coder:14b
Start-Sleep -Seconds 2

$qwenBody = @{
    model = "qwen2.5-coder:14b"
    prompt = "Say 'Context loaded successfully' in one sentence"
    stream = $false
    options = @{
        num_ctx = 131072
        num_predict = 20
    }
} | ConvertTo-Json

Write-Host "Sending request with num_ctx=131072..." -ForegroundColor Gray
$qwenStart = Get-Date
try {
    $response = Invoke-RestMethod -Uri "http://localhost:11434/api/generate" `
        -Method Post -Body $qwenBody -ContentType "application/json" -TimeoutSec 120
    $qwenDuration = ((Get-Date) - $qwenStart).TotalSeconds
    Write-Host "✅ Qwen loaded in $([math]::Round($qwenDuration, 2))s" -ForegroundColor Green
    Write-Host "Response: $($response.response)" -ForegroundColor Gray
} catch {
    Write-Host "❌ Qwen failed: $($_.Exception.Message)" -ForegroundColor Red
}

Start-Sleep -Seconds 2
Write-Host "`nChecking Qwen context..." -ForegroundColor Gray
ollama ps | Select-String "qwen"

# 2. Test Llava at 32k
Write-Host "`n[2] Testing Llava at 32k context..." -ForegroundColor Yellow
ollama stop llava:latest
Start-Sleep -Seconds 2

$llavaBody = @{
    model = "llava:latest"
    prompt = "Say 'Context loaded successfully' in one sentence"
    stream = $false
    options = @{
        num_ctx = 32768
        num_predict = 20
    }
} | ConvertTo-Json

Write-Host "Sending request with num_ctx=32768..." -ForegroundColor Gray
$llavaStart = Get-Date
try {
    $response = Invoke-RestMethod -Uri "http://localhost:11434/api/generate" `
        -Method Post -Body $llavaBody -ContentType "application/json" -TimeoutSec 120
    $llavaDuration = ((Get-Date) - $llavaStart).TotalSeconds
    Write-Host "✅ Llava loaded in $([math]::Round($llavaDuration, 2))s" -ForegroundColor Green
    Write-Host "Response: $($response.response)" -ForegroundColor Gray
} catch {
    Write-Host "❌ Llava failed: $($_.Exception.Message)" -ForegroundColor Red
}

Start-Sleep -Seconds 2
Write-Host "`nChecking Llava context..." -ForegroundColor Gray
ollama ps | Select-String "llava"

# 3. Test Phi4 (should already be at 128k)
Write-Host "`n[3] Verifying Phi4 at 128k context..." -ForegroundColor Yellow
ollama stop phi4:latest
Start-Sleep -Seconds 2

$phi4Body = @{
    model = "phi4:latest"
    prompt = "Say 'Context loaded successfully' in one sentence"
    stream = $false
    options = @{
        num_ctx = 131072
        num_predict = 20
    }
} | ConvertTo-Json

Write-Host "Sending request with num_ctx=131072..." -ForegroundColor Gray
$phi4Start = Get-Date
try {
    $response = Invoke-RestMethod -Uri "http://localhost:11434/api/generate" `
        -Method Post -Body $phi4Body -ContentType "application/json" -TimeoutSec 120
    $phi4Duration = ((Get-Date) - $phi4Start).TotalSeconds
    Write-Host "✅ Phi4 loaded in $([math]::Round($phi4Duration, 2))s" -ForegroundColor Green
    Write-Host "Response: $($response.response)" -ForegroundColor Gray
} catch {
    Write-Host "❌ Phi4 failed: $($_.Exception.Message)" -ForegroundColor Red
}

Start-Sleep -Seconds 2
Write-Host "`nChecking Phi4 context..." -ForegroundColor Gray
ollama ps | Select-String "phi4"

# Final status
Write-Host "`n=== FINAL OLLAMA STATUS ===" -ForegroundColor Cyan
ollama ps

Write-Host "`n=== EXPECTED VALUES ===" -ForegroundColor Cyan
Write-Host "Qwen 2.5 Coder: 131072 (128k)" -ForegroundColor White
Write-Host "Llava: 32768 (32k)" -ForegroundColor White
Write-Host "Phi4: 131072 (128k)" -ForegroundColor White
Write-Host "Gemma3: 131072 (128k)" -ForegroundColor White

