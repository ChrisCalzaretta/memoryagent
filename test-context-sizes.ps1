# ========================================
# OLLAMA CONTEXT SIZE TEST SCRIPT
# Tests different context sizes for each model
# ========================================

$ErrorActionPreference = "Continue"
$ProgressPreference = "SilentlyContinue"

# Test configuration
$models = @(
    @{ Name = "qwen2.5-coder:14b"; Sizes = @(32768, 64000, 96000, 128000) },
    @{ Name = "phi4:latest"; Sizes = @(32768, 64000, 96000, 128000) },
    @{ Name = "deepseek-coder-v2:16b"; Sizes = @(32768, 64000) },  # Only test 32k and 64k (we know 64k crashes)
    @{ Name = "gemma3:latest"; Sizes = @(32768, 64000, 96000, 128000, 131072) }
)

$testPrompt = "Write a simple Hello World function in C#. Keep it brief."
$results = @()

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "OLLAMA CONTEXT SIZE TEST" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

# Function to test a model at a specific context size
function Test-ModelContext {
    param(
        [string]$ModelName,
        [int]$ContextSize
    )
    
    Write-Host "`n[$ModelName @ ${ContextSize}k]" -ForegroundColor Yellow
    Write-Host "Unloading model..." -ForegroundColor Gray
    
    # Unload the model first
    try {
        ollama stop $ModelName 2>$null | Out-Null
        Start-Sleep -Seconds 2
    } catch {}
    
    # Create request body
    $body = @{
        model = $ModelName
        prompt = $testPrompt
        stream = $false
        options = @{
            num_ctx = $ContextSize
            temperature = 0.7
            num_predict = 100
        }
    } | ConvertTo-Json -Depth 5
    
    Write-Host "Testing context=$ContextSize..." -ForegroundColor Gray
    
    $startTime = Get-Date
    $success = $false
    $error = $null
    $duration = 0
    
    try {
        $response = Invoke-RestMethod -Uri "http://localhost:11434/api/generate" `
            -Method Post `
            -Body $body `
            -ContentType "application/json" `
            -TimeoutSec 120
        
        $endTime = Get-Date
        $duration = ($endTime - $startTime).TotalSeconds
        
        if ($response.response) {
            $success = $true
            Write-Host "✅ SUCCESS in $([math]::Round($duration, 2))s" -ForegroundColor Green
        } else {
            $error = "No response received"
            Write-Host "❌ FAILED: $error" -ForegroundColor Red
        }
    }
    catch {
        $endTime = Get-Date
        $duration = ($endTime - $startTime).TotalSeconds
        $error = $_.Exception.Message
        Write-Host "❌ FAILED: $error" -ForegroundColor Red
    }
    
    # Check VRAM usage
    Write-Host "Checking VRAM..." -ForegroundColor Gray
    Start-Sleep -Seconds 2
    $vramInfo = ollama ps | Out-String
    
    return @{
        Model = $ModelName
        ContextSize = $ContextSize
        Success = $success
        Duration = [math]::Round($duration, 2)
        Error = $error
        Timestamp = Get-Date
    }
}

# ========================================
# TEST 1: Individual Model Tests
# ========================================
Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "TEST 1: Individual Model Context Tests" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

foreach ($model in $models) {
    Write-Host "`n--- Testing $($model.Name) ---" -ForegroundColor Magenta
    
    foreach ($size in $model.Sizes) {
        $result = Test-ModelContext -ModelName $model.Name -ContextSize $size
        $results += $result
        
        # Small delay between tests
        Start-Sleep -Seconds 3
    }
}

# ========================================
# TEST 2: All Models at Same Context
# ========================================
Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "TEST 2: All Models Together (32k)" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

Write-Host "`nUnloading all models..." -ForegroundColor Gray
ollama stop qwen2.5-coder:14b 2>$null | Out-Null
ollama stop phi4:latest 2>$null | Out-Null
ollama stop deepseek-coder-v2:16b 2>$null | Out-Null
ollama stop gemma3:latest 2>$null | Out-Null
Start-Sleep -Seconds 5

Write-Host "Loading models sequentially at 32k context..." -ForegroundColor Gray

# Load them one by one
$result1 = Test-ModelContext -ModelName "qwen2.5-coder:14b" -ContextSize 32768
Start-Sleep -Seconds 2

$result2 = Test-ModelContext -ModelName "phi4:latest" -ContextSize 32768
Start-Sleep -Seconds 2

$result3 = Test-ModelContext -ModelName "deepseek-coder-v2:16b" -ContextSize 32768
Start-Sleep -Seconds 2

$result4 = Test-ModelContext -ModelName "gemma3:latest" -ContextSize 32768

# Check final VRAM state
Write-Host "`n📊 Final VRAM State (All Models at 32k):" -ForegroundColor Cyan
ollama ps

# ========================================
# TEST 3: All Models at Max Safe Context
# ========================================
Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "TEST 3: All Models at Max Context" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

Write-Host "`nUnloading all models..." -ForegroundColor Gray
ollama stop qwen2.5-coder:14b 2>$null | Out-Null
ollama stop phi4:latest 2>$null | Out-Null
ollama stop deepseek-coder-v2:16b 2>$null | Out-Null
ollama stop gemma3:latest 2>$null | Out-Null
Start-Sleep -Seconds 5

Write-Host "Loading models at max context..." -ForegroundColor Gray

# Load at max tested sizes
$result1 = Test-ModelContext -ModelName "qwen2.5-coder:14b" -ContextSize 128000
Start-Sleep -Seconds 2

$result2 = Test-ModelContext -ModelName "phi4:latest" -ContextSize 128000
Start-Sleep -Seconds 2

$result3 = Test-ModelContext -ModelName "deepseek-coder-v2:16b" -ContextSize 32768
Start-Sleep -Seconds 2

$result4 = Test-ModelContext -ModelName "gemma3:latest" -ContextSize 131072

# Check final VRAM state
Write-Host "`n📊 Final VRAM State (All Models at Max):" -ForegroundColor Cyan
ollama ps

# ========================================
# RESULTS SUMMARY
# ========================================
Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "TEST RESULTS SUMMARY" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

# Group results by model
$groupedResults = $results | Group-Object -Property Model

foreach ($group in $groupedResults) {
    Write-Host "`n$($group.Name):" -ForegroundColor Magenta
    Write-Host ("=" * 60) -ForegroundColor Gray
    
    $group.Group | ForEach-Object {
        $status = if ($_.Success) { "✅" } else { "❌" }
        $contextK = [math]::Round($_.ContextSize / 1024, 0)
        $line = "$status ${contextK}k context: $($_.Duration)s"
        
        if ($_.Error) {
            $line += " - ERROR: $($_.Error)"
        }
        
        $color = if ($_.Success) { "Green" } else { "Red" }
        Write-Host "  $line" -ForegroundColor $color
    }
}

# ========================================
# RECOMMENDATIONS
# ========================================
Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "RECOMMENDATIONS" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

# Find max safe context for each model
foreach ($group in $groupedResults) {
    $maxSafe = $group.Group | Where-Object { $_.Success } | 
               Sort-Object -Property ContextSize -Descending | 
               Select-Object -First 1
    
    if ($maxSafe) {
        $contextK = [math]::Round($maxSafe.ContextSize / 1024, 0)
        Write-Host "✅ $($group.Name): ${contextK}k (tested in $($maxSafe.Duration)s)" -ForegroundColor Green
    } else {
        Write-Host "❌ $($group.Name): All tests failed" -ForegroundColor Red
    }
}

# Save results to JSON
$results | ConvertTo-Json -Depth 5 | Out-File "context-test-results.json"
Write-Host "`n📄 Detailed results saved to: context-test-results.json" -ForegroundColor Cyan

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "TEST COMPLETE!" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

