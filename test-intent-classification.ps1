#!/usr/bin/env pwsh
# Test Intent Classification Feature

Write-Host "`n🧠 Testing LLM-Powered Intent Classification`n" -ForegroundColor Cyan
Write-Host "=" * 60 -ForegroundColor Cyan

# Test 1: SmartSearch with Flutter Performance Intent
Write-Host "`n📝 Test 1: SmartSearch with User Goal" -ForegroundColor Yellow
Write-Host "-" * 60

$test1 = @{
    method = "tools/call"
    params = @{
        name = "smartsearch"
        arguments = @{
            query = "How does caching work?"
            context = "MemoryAgent"
            user_goal = "I want to improve performance in my Flutter mobile app"
            limit = 3
        }
    }
} | ConvertTo-Json -Depth 10

Write-Host "Request:" -ForegroundColor Gray
Write-Host $test1 -ForegroundColor DarkGray

try {
    $response1 = Invoke-RestMethod -Uri "http://localhost:5000/api/mcp" `
        -Method POST `
        -Body $test1 `
        -ContentType "application/json" `
        -ErrorAction Stop
    
    Write-Host "`n✅ Response received:" -ForegroundColor Green
    $response1 | ConvertTo-Json -Depth 10 | Write-Host
} catch {
    Write-Host "`n❌ Error: $_" -ForegroundColor Red
    Write-Host "Status: $($_.Exception.Response.StatusCode)" -ForegroundColor Red
}

Start-Sleep -Seconds 2

# Test 2: Get Recommendations with E-Commerce Security Intent
Write-Host "`n`n📝 Test 2: Get Recommendations with User Goal" -ForegroundColor Yellow
Write-Host "-" * 60

$test2 = @{
    method = "tools/call"
    params = @{
        name = "get_recommendations"
        arguments = @{
            context = "MemoryAgent"
            user_goal = "Build a secure Flutter e-commerce app with excellent performance"
            maxRecommendations = 5
            includeLowPriority = $false
        }
    }
} | ConvertTo-Json -Depth 10

Write-Host "Request:" -ForegroundColor Gray
Write-Host $test2 -ForegroundColor DarkGray

try {
    $response2 = Invoke-RestMethod -Uri "http://localhost:5000/api/mcp" `
        -Method POST `
        -Body $test2 `
        -ContentType "application/json" `
        -ErrorAction Stop
    
    Write-Host "`n✅ Response received:" -ForegroundColor Green
    $response2 | ConvertTo-Json -Depth 10 | Write-Host
} catch {
    Write-Host "`n❌ Error: $_" -ForegroundColor Red
}

Start-Sleep -Seconds 2

# Test 3: Search Patterns with AI Agent Intent
Write-Host "`n`n📝 Test 3: Search Patterns with AI Agent Goal" -ForegroundColor Yellow
Write-Host "-" * 60

$test3 = @{
    method = "tools/call"
    params = @{
        name = "search_patterns"
        arguments = @{
            query = "agent patterns"
            context = "MemoryAgent"
            user_goal = "Migrate from AutoGen to Agent Framework"
            limit = 5
        }
    }
} | ConvertTo-Json -Depth 10

Write-Host "Request:" -ForegroundColor Gray
Write-Host $test3 -ForegroundColor DarkGray

try {
    $response3 = Invoke-RestMethod -Uri "http://localhost:5000/api/mcp" `
        -Method POST `
        -Body $test3 `
        -ContentType "application/json" `
        -ErrorAction Stop
    
    Write-Host "`n✅ Response received:" -ForegroundColor Green
    $response3 | ConvertTo-Json -Depth 10 | Write-Host
} catch {
    Write-Host "`n❌ Error: $_" -ForegroundColor Red
}

# Check Server Logs
Write-Host "`n`n📋 Server Logs (Intent Classification)" -ForegroundColor Yellow
Write-Host "-" * 60

Write-Host "`nSearching for intent classification in logs...`n" -ForegroundColor Gray
docker logs memory-agent-server --tail 100 2>&1 | Select-String -Pattern "Intent|classified|🎯|🧠|DeepSeek|ClassifyIntentAsync" -Context 1,2

Write-Host "`n`n✅ Test Complete!" -ForegroundColor Green
Write-Host "=" * 60 -ForegroundColor Cyan

