# Test script to verify background execution returns immediately

Write-Host "🧪 Testing MemoryRouter background execution..." -ForegroundColor Cyan
Write-Host ""

# Test 1: Background mode (default, should return immediately)
Write-Host "📋 Test 1: Background mode (should return in < 2 seconds)" -ForegroundColor Yellow
$sw = [System.Diagnostics.Stopwatch]::StartNew()

$body = @{
    jsonrpc = "2.0"
    id = 1
    method = "tools/call"
    params = @{
        name = "execute_task"
        arguments = @{
            request = "Find authentication code"
            # background defaults to true, so we don't need to specify it
        }
    }
} | ConvertTo-Json -Depth 10

try {
    $response = Invoke-RestMethod -Uri "http://localhost:5010/api/mcp" -Method Post -Body $body -ContentType "application/json" -TimeoutSec 30
    $sw.Stop()
    
    Write-Host "✅ Response received in $($sw.ElapsedMilliseconds)ms" -ForegroundColor Green
    Write-Host ""
    Write-Host "Response:" -ForegroundColor Cyan
    $response | ConvertTo-Json -Depth 5
    Write-Host ""
    
    if ($sw.ElapsedMilliseconds -lt 2000) {
        Write-Host "✅ PASS: Returned immediately (< 2s)" -ForegroundColor Green
    } else {
        Write-Host "❌ FAIL: Took too long ($($sw.ElapsedMilliseconds)ms)" -ForegroundColor Red
    }
} catch {
    $sw.Stop()
    Write-Host "❌ Request failed after $($sw.ElapsedMilliseconds)ms" -ForegroundColor Red
    Write-Host "Error: $_" -ForegroundColor Red
}

Write-Host ""
Write-Host "=" * 80
Write-Host ""

# Test 2: Explicit background=false (should wait for result)
Write-Host "📋 Test 2: Synchronous mode (background=false, may take longer)" -ForegroundColor Yellow
$sw2 = [System.Diagnostics.Stopwatch]::StartNew()

$body2 = @{
    jsonrpc = "2.0"
    id = 2
    method = "tools/call"
    params = @{
        name = "execute_task"
        arguments = @{
            request = "List available tools"
            background = $false
        }
    }
} | ConvertTo-Json -Depth 10

try {
    $response2 = Invoke-RestMethod -Uri "http://localhost:5010/api/mcp" -Method Post -Body $body2 -ContentType "application/json" -TimeoutSec 60
    $sw2.Stop()
    
    Write-Host "✅ Response received in $($sw2.ElapsedMilliseconds)ms" -ForegroundColor Green
    Write-Host ""
    Write-Host "Response includes results: $($response2.result.content[0].text.Length) characters" -ForegroundColor Cyan
    Write-Host ""
    
    if ($response2.result.content[0].text -match "✅ Task Completed Successfully") {
        Write-Host "✅ PASS: Got full results (synchronous mode working)" -ForegroundColor Green
    } else {
        Write-Host "⚠️  WARNING: Response format unexpected" -ForegroundColor Yellow
    }
} catch {
    $sw2.Stop()
    Write-Host "❌ Request failed after $($sw2.ElapsedMilliseconds)ms" -ForegroundColor Red
    Write-Host "Error: $_" -ForegroundColor Red
}

Write-Host ""
Write-Host "🏁 Tests complete!" -ForegroundColor Cyan

