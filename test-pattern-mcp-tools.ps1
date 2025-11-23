# Test script for Pattern Detection MCP Tools
# Tests all 4 new MCP endpoints for pattern detection

$ErrorActionPreference = "Continue"
$baseUrl = "http://localhost:5098"
$context = "CBC_AI" # Change this to your project context

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Testing Pattern Detection MCP Tools" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Test 1: Search Patterns
Write-Host "TEST 1: Search for Caching Patterns" -ForegroundColor Yellow
Write-Host "------------------------------------" -ForegroundColor Yellow
try {
    $response = Invoke-RestMethod -Uri "$baseUrl/mcp" `
        -Method POST `
        -ContentType "application/json" `
        -Body (@{
            jsonrpc = "2.0"
            id = 1
            method = "tools/call"
            params = @{
                name = "search_patterns"
                arguments = @{
                    query = "caching patterns"
                    context = $context
                    limit = 5
                }
            }
        } | ConvertTo-Json -Depth 10)
    
    Write-Host "✅ Success!" -ForegroundColor Green
    Write-Host ($response.result.content[0].text | Out-String)
} catch {
    Write-Host "❌ Error: $($_.Exception.Message)" -ForegroundColor Red
}
Write-Host ""

# Test 2: Search for Retry Logic Patterns
Write-Host "TEST 2: Search for Retry Logic Patterns" -ForegroundColor Yellow
Write-Host "------------------------------------" -ForegroundColor Yellow
try {
    $response = Invoke-RestMethod -Uri "$baseUrl/mcp" `
        -Method POST `
        -ContentType "application/json" `
        -Body (@{
            jsonrpc = "2.0"
            id = 2
            method = "tools/call"
            params = @{
                name = "search_patterns"
                arguments = @{
                    query = "retry logic and resilience"
                    context = $context
                    limit = 5
                }
            }
        } | ConvertTo-Json -Depth 10)
    
    Write-Host "✅ Success!" -ForegroundColor Green
    Write-Host ($response.result.content[0].text | Out-String)
} catch {
    Write-Host "❌ Error: $($_.Exception.Message)" -ForegroundColor Red
}
Write-Host ""

# Test 3: Get Available Best Practices
Write-Host "TEST 3: Get Available Best Practices" -ForegroundColor Yellow
Write-Host "------------------------------------" -ForegroundColor Yellow
try {
    $response = Invoke-RestMethod -Uri "$baseUrl/mcp" `
        -Method POST `
        -ContentType "application/json" `
        -Body (@{
            jsonrpc = "2.0"
            id = 3
            method = "tools/call"
            params = @{
                name = "get_available_best_practices"
                arguments = @{}
            }
        } | ConvertTo-Json -Depth 10)
    
    Write-Host "✅ Success!" -ForegroundColor Green
    Write-Host ($response.result.content[0].text | Out-String)
} catch {
    Write-Host "❌ Error: $($_.Exception.Message)" -ForegroundColor Red
}
Write-Host ""

# Test 4: Validate Best Practices (All)
Write-Host "TEST 4: Validate All Best Practices" -ForegroundColor Yellow
Write-Host "------------------------------------" -ForegroundColor Yellow
try {
    $response = Invoke-RestMethod -Uri "$baseUrl/mcp" `
        -Method POST `
        -ContentType "application/json" `
        -Body (@{
            jsonrpc = "2.0"
            id = 4
            method = "tools/call"
            params = @{
                name = "validate_best_practices"
                arguments = @{
                    context = $context
                    includeExamples = $true
                    maxExamplesPerPractice = 3
                }
            }
        } | ConvertTo-Json -Depth 10)
    
    Write-Host "✅ Success!" -ForegroundColor Green
    Write-Host ($response.result.content[0].text | Out-String)
} catch {
    Write-Host "❌ Error: $($_.Exception.Message)" -ForegroundColor Red
}
Write-Host ""

# Test 5: Validate Specific Best Practices
Write-Host "TEST 5: Validate Specific Best Practices (Caching, Retry, Validation)" -ForegroundColor Yellow
Write-Host "------------------------------------" -ForegroundColor Yellow
try {
    $response = Invoke-RestMethod -Uri "$baseUrl/mcp" `
        -Method POST `
        -ContentType "application/json" `
        -Body (@{
            jsonrpc = "2.0"
            id = 5
            method = "tools/call"
            params = @{
                name = "validate_best_practices"
                arguments = @{
                    context = $context
                    bestPractices = @("cache-aside", "retry-logic", "input-validation")
                    includeExamples = $true
                    maxExamplesPerPractice = 2
                }
            }
        } | ConvertTo-Json -Depth 10)
    
    Write-Host "✅ Success!" -ForegroundColor Green
    Write-Host ($response.result.content[0].text | Out-String)
} catch {
    Write-Host "❌ Error: $($_.Exception.Message)" -ForegroundColor Red
}
Write-Host ""

# Test 6: Get Recommendations
Write-Host "TEST 6: Get Architecture Recommendations" -ForegroundColor Yellow
Write-Host "------------------------------------" -ForegroundColor Yellow
try {
    $response = Invoke-RestMethod -Uri "$baseUrl/mcp" `
        -Method POST `
        -ContentType "application/json" `
        -Body (@{
            jsonrpc = "2.0"
            id = 6
            method = "tools/call"
            params = @{
                name = "get_recommendations"
                arguments = @{
                    context = $context
                    includeLowPriority = $false
                    maxRecommendations = 10
                }
            }
        } | ConvertTo-Json -Depth 10)
    
    Write-Host "✅ Success!" -ForegroundColor Green
    Write-Host ($response.result.content[0].text | Out-String)
} catch {
    Write-Host "❌ Error: $($_.Exception.Message)" -ForegroundColor Red
}
Write-Host ""

# Test 7: Get Recommendations for Specific Categories
Write-Host "TEST 7: Get Recommendations (Security & Performance only)" -ForegroundColor Yellow
Write-Host "------------------------------------" -ForegroundColor Yellow
try {
    $response = Invoke-RestMethod -Uri "$baseUrl/mcp" `
        -Method POST `
        -ContentType "application/json" `
        -Body (@{
            jsonrpc = "2.0"
            id = 7
            method = "tools/call"
            params = @{
                name = "get_recommendations"
                arguments = @{
                    context = $context
                    categories = @("Security", "Performance")
                    includeLowPriority = $false
                    maxRecommendations = 5
                }
            }
        } | ConvertTo-Json -Depth 10)
    
    Write-Host "✅ Success!" -ForegroundColor Green
    Write-Host ($response.result.content[0].text | Out-String)
} catch {
    Write-Host "❌ Error: $($_.Exception.Message)" -ForegroundColor Red
}
Write-Host ""

# Test 8: Search for Validation Patterns
Write-Host "TEST 8: Search for Validation Patterns" -ForegroundColor Yellow
Write-Host "------------------------------------" -ForegroundColor Yellow
try {
    $response = Invoke-RestMethod -Uri "$baseUrl/mcp" `
        -Method POST `
        -ContentType "application/json" `
        -Body (@{
            jsonrpc = "2.0"
            id = 8
            method = "tools/call"
            params = @{
                name = "search_patterns"
                arguments = @{
                    query = "input validation DataAnnotations FluentValidation"
                    context = $context
                    limit = 10
                }
            }
        } | ConvertTo-Json -Depth 10)
    
    Write-Host "✅ Success!" -ForegroundColor Green
    Write-Host ($response.result.content[0].text | Out-String)
} catch {
    Write-Host "❌ Error: $($_.Exception.Message)" -ForegroundColor Red
}
Write-Host ""

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "All Tests Complete!" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

