#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Test the two fixes: search_patterns and validate_project
#>

param(
    [string]$McpUrl = "http://localhost:5098",
    [string]$Context = "cbcai"
)

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Testing Fixes for search_patterns and validate_project" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

function Test-McpTool {
    param(
        [string]$Name,
        [object]$Arguments,
        [string]$Description
    )

    Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor DarkGray
    Write-Host "🔍 Test: $Description" -ForegroundColor Yellow
    Write-Host "   Tool: $Name" -ForegroundColor Gray
    Write-Host ""

    try {
        $body = @{
            jsonrpc = "2.0"
            id      = 1
            method  = "tools/call"
            params  = @{
                name      = $Name
                arguments = $Arguments
            }
        } | ConvertTo-Json -Depth 10

        $response = Invoke-RestMethod -Uri "$McpUrl/mcp" -Method Post `
            -Body $body -ContentType "application/json" -TimeoutSec 30

        if ($response.result.isError) {
            Write-Host "❌ FAILED: $($response.result.content[0].text)" -ForegroundColor Red
            return $false
        } else {
            $resultText = $response.result.content[0].text
            Write-Host $resultText -ForegroundColor Green
            Write-Host ""
            Write-Host "✅ SUCCESS" -ForegroundColor Green
            return $true
        }
    }
    catch {
        Write-Host "❌ ERROR: $($_.Exception.Message)" -ForegroundColor Red
        return $false
    }
}

# Test 1: search_patterns (JSON casting issue - FIXED)
Write-Host ""
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
Write-Host "🔧 Test 1: search_patterns (JSON Casting Fix)" -ForegroundColor Cyan
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan

$test1 = Test-McpTool -Name "search_patterns" -Arguments @{
    query = "caching"
    context = $Context
    limit = 5
} -Description "Search for caching patterns (JSON casting should be fixed)"

# Test 2: validate_project (empty collection issue - FIXED)
Write-Host ""
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
Write-Host "🔧 Test 2: validate_project (Empty Collection Fix)" -ForegroundColor Cyan
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan

$test2 = Test-McpTool -Name "validate_project" -Arguments @{
    context = $Context
} -Description "Validate project (should handle empty patterns gracefully)"

# Summary
Write-Host ""
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
Write-Host "📊 FIX TEST RESULTS" -ForegroundColor Cyan
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
Write-Host ""

if ($test1) {
    Write-Host "✅ search_patterns: FIXED!" -ForegroundColor Green
} else {
    Write-Host "❌ search_patterns: Still failing" -ForegroundColor Red
}

if ($test2) {
    Write-Host "✅ validate_project: FIXED!" -ForegroundColor Green
} else {
    Write-Host "❌ validate_project: Still failing" -ForegroundColor Red
}

Write-Host ""

if ($test1 -and $test2) {
    Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Green
    Write-Host "🎉 ALL FIXES VERIFIED! Both issues resolved!" -ForegroundColor Green
    Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Green
} else {
    Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Yellow
    Write-Host "⚠️  Some fixes still need work" -ForegroundColor Yellow
    Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Yellow
}

