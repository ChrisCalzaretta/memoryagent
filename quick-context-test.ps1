# Quick Context Test - Tests critical sizes only
$ErrorActionPreference = "Continue"

$tests = @(
    @{ Model = "qwen2.5-coder:14b"; Context = 128000; Name = "Qwen @ 128k" },
    @{ Model = "phi4:latest"; Context = 128000; Name = "Phi4 @ 128k" },
    @{ Model = "deepseek-coder-v2:16b"; Context = 64000; Name = "DeepSeek @ 64k (crash test)" },
    @{ Model = "gemma3:latest"; Context = 131072; Name = "Gemma3 @ 131k" }
)

$prompt = "Write a hello world in C#"

Write-Host "`n=== QUICK CONTEXT TEST ===" -ForegroundColor Cyan
Write-Host "Testing max context for each model`n" -ForegroundColor Cyan

foreach ($test in $tests) {
    Write-Host "Testing: $($test.Name)" -ForegroundColor Yellow
    
    # Unload first
    ollama stop $test.Model 2>$null | Out-Null
    Start-Sleep -Seconds 2
    
    # Build request
    $body = @{
        model = $test.Model
        prompt = $prompt
        stream = $false
        options = @{ num_ctx = $test.Context; num_predict = 50 }
    } | ConvertTo-Json -Depth 5
    
    $start = Get-Date
    try {
        $response = Invoke-RestMethod -Uri "http://localhost:11434/api/generate" `
            -Method Post -Body $body -ContentType "application/json" -TimeoutSec 60
        
        $duration = ((Get-Date) - $start).TotalSeconds
        Write-Host "  ✅ SUCCESS in $([math]::Round($duration, 2))s" -ForegroundColor Green
    }
    catch {
        $duration = ((Get-Date) - $start).TotalSeconds
        Write-Host "  ❌ FAILED: $($_.Exception.Message)" -ForegroundColor Red
    }
    
    Start-Sleep -Seconds 2
}

Write-Host "`n=== VRAM CHECK ===" -ForegroundColor Cyan
ollama ps

Write-Host "`nTest complete!" -ForegroundColor Green

