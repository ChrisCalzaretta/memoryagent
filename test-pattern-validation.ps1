#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Test Pattern Validation MCP Tools

.DESCRIPTION
    Tests all 5 new pattern validation MCP tools:
    1. validate_pattern_quality - Deep validation of pattern implementation
    2. find_anti_patterns - Find poorly implemented patterns
    3. validate_security - Security audit
    4. get_migration_path - Migration guidance for legacy patterns
    5. validate_project - Comprehensive project validation
#>

param(
    [string]$McpUrl = "http://localhost:5098",
    [string]$Context = "cbcai"
)

$ErrorActionPreference = "Continue"
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Pattern Validation MCP Tools Test" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Test results tracking
$testResults = @()

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
            $script:testResults += @{Tool = $Name; Status = "FAILED"; Error = $response.result.content[0].text}
            return $false
        } else {
            $resultText = $response.result.content[0].text
            Write-Host $resultText -ForegroundColor Green
            Write-Host ""
            Write-Host "✅ SUCCESS" -ForegroundColor Green
            $script:testResults += @{Tool = $Name; Status = "SUCCESS"}
            return $true
        }
    }
    catch {
        Write-Host "❌ ERROR: $($_.Exception.Message)" -ForegroundColor Red
        $script:testResults += @{Tool = $Name; Status = "ERROR"; Error = $_.Exception.Message}
        return $false
    }
}

# Wait for services
Write-Host "⏳ Waiting for services to be ready..." -ForegroundColor Cyan
Start-Sleep -Seconds 5

# Test 1: List all MCP tools (verify new tools exist)
Write-Host ""
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
Write-Host "📋 Step 1: Verify New MCP Tools Exist" -ForegroundColor Cyan
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan

try {
    $body = @{
        jsonrpc = "2.0"
        id      = 1
        method  = "tools/list"
    } | ConvertTo-Json

    $response = Invoke-RestMethod -Uri "$McpUrl/mcp" -Method Post `
        -Body $body -ContentType "application/json"

    $toolNames = $response.result.tools | ForEach-Object { $_.name }
    
    $expectedTools = @(
        "validate_pattern_quality",
        "find_anti_patterns",
        "validate_security",
        "get_migration_path",
        "validate_project"
    )

    foreach ($tool in $expectedTools) {
        if ($toolNames -contains $tool) {
            Write-Host "  ✅ $tool" -ForegroundColor Green
        } else {
            Write-Host "  ❌ $tool NOT FOUND" -ForegroundColor Red
        }
    }
}
catch {
    Write-Host "❌ Error listing tools: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""

# Test 2: Search for patterns first (to get pattern IDs)
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
Write-Host "📋 Step 2: Search for Patterns (Get IDs)" -ForegroundColor Cyan
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan

$patternId = $null

Test-McpTool -Name "search_patterns" -Arguments @{
    query = "caching"
    context = $Context
    limit = 5
} -Description "Search for caching patterns to get a pattern ID"

# Test 3: Validate Pattern Quality
Write-Host ""
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
Write-Host "🔍 Step 3: Validate Pattern Quality" -ForegroundColor Cyan
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan

# Since we might not have a real pattern ID yet, use a sample one
Test-McpTool -Name "validate_pattern_quality" -Arguments @{
    pattern_id = "caching_pattern_sample"
    context = $Context
    include_auto_fix = $true
    min_severity = "low"
} -Description "Validate quality of a caching pattern"

# Test 4: Find Anti-Patterns
Write-Host ""
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
Write-Host "🚨 Step 4: Find Anti-Patterns" -ForegroundColor Cyan
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan

Test-McpTool -Name "find_anti_patterns" -Arguments @{
    context = $Context
    min_severity = "medium"
    include_legacy = $true
} -Description "Find all anti-patterns and legacy patterns in project"

# Test 5: Validate Security
Write-Host ""
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
Write-Host "🔒 Step 5: Validate Security" -ForegroundColor Cyan
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan

Test-McpTool -Name "validate_security" -Arguments @{
    context = $Context
} -Description "Run security audit on all patterns"

# Test 6: Get Migration Path (for AutoGen patterns if any)
Write-Host ""
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
Write-Host "🔄 Step 6: Get Migration Path" -ForegroundColor Cyan
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan

# Search for AutoGen patterns first
Write-Host "   Searching for AutoGen patterns..." -ForegroundColor Gray
Test-McpTool -Name "search_patterns" -Arguments @{
    query = "AutoGen"
    context = $Context
    limit = 3
} -Description "Search for AutoGen patterns"

# Try to get migration path (might not have AutoGen patterns)
Test-McpTool -Name "get_migration_path" -Arguments @{
    pattern_id = "autogen_pattern_sample"
    include_code_example = $true
} -Description "Get migration path for a legacy pattern"

# Test 7: Validate Entire Project
Write-Host ""
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
Write-Host "📊 Step 7: Comprehensive Project Validation" -ForegroundColor Cyan
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan

Test-McpTool -Name "validate_project" -Arguments @{
    context = $Context
} -Description "Run comprehensive project validation"

# Test 8: Check existing recommendation tools
Write-Host ""
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
Write-Host "💡 Step 8: Test Pattern Recommendations" -ForegroundColor Cyan
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan

Test-McpTool -Name "get_recommendations" -Arguments @{
    context = $Context
    include_low_priority = $false
    max_recommendations = 10
} -Description "Get pattern recommendations for project"

# Test 9: Validate Best Practices
Write-Host ""
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
Write-Host "📚 Step 9: Validate Best Practices" -ForegroundColor Cyan
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan

Test-McpTool -Name "validate_best_practices" -Arguments @{
    context = $Context
    include_examples = $true
    max_examples_per_practice = 3
} -Description "Validate Azure best practices implementation"

# Final Report
Write-Host ""
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
Write-Host "📊 TEST SUMMARY" -ForegroundColor Cyan
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
Write-Host ""

$successCount = ($testResults | Where-Object { $_.Status -eq "SUCCESS" }).Count
$failCount = ($testResults | Where-Object { $_.Status -ne "SUCCESS" }).Count
$totalTests = $testResults.Count

Write-Host "Total Tests: $totalTests" -ForegroundColor White
Write-Host "✅ Passed: $successCount" -ForegroundColor Green
Write-Host "❌ Failed: $failCount" -ForegroundColor Red
Write-Host ""

if ($failCount -gt 0) {
    Write-Host "Failed Tests:" -ForegroundColor Red
    foreach ($result in $testResults | Where-Object { $_.Status -ne "SUCCESS" }) {
        Write-Host "  • $($result.Tool): $($result.Error)" -ForegroundColor Red
    }
}

Write-Host ""
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
Write-Host "✅ Pattern Validation Test Complete!" -ForegroundColor Green
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan

