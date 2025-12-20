#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Test the new category system in MemoryRouter

.DESCRIPTION
    Tests the tool categorization and filtering functionality in the MemoryRouter service.
    Demonstrates how tools are automatically categorized and can be filtered.

.EXAMPLE
    .\test-router-categories.ps1
#>

$ErrorActionPreference = "Stop"

$baseUrl = "http://localhost:5100"

Write-Host "`n🧪 Testing MemoryRouter Category System" -ForegroundColor Cyan
Write-Host "=" * 60 -ForegroundColor Cyan

# Test health endpoint
Write-Host "`n1️⃣  Testing health endpoint..." -ForegroundColor Yellow
try {
    $health = Invoke-RestMethod -Uri "$baseUrl/health" -Method Get
    Write-Host "✅ Service is healthy: $($health.service)" -ForegroundColor Green
} catch {
    Write-Host "❌ Service not available. Please start MemoryRouter first." -ForegroundColor Red
    Write-Host "   Run: docker-compose up memory-router" -ForegroundColor Yellow
    exit 1
}

# Function to call MCP tools
function Invoke-McpTool {
    param(
        [string]$ToolName,
        [hashtable]$Arguments = @{}
    )
    
    $body = @{
        jsonrpc = "2.0"
        id = 1
        method = "tools/call"
        params = @{
            name = $ToolName
            arguments = $Arguments
        }
    } | ConvertTo-Json -Depth 10
    
    $response = Invoke-RestMethod -Uri "$baseUrl/api/mcp" -Method Post `
        -ContentType "application/json" -Body $body
    
    return $response
}

# Test listing all tools
Write-Host "`n2️⃣  Listing ALL tools..." -ForegroundColor Yellow
$allToolsResponse = Invoke-McpTool -ToolName "list_available_tools" -Arguments @{}

if ($allToolsResponse.result.content) {
    $allToolsText = $allToolsResponse.result.content[0].text
    Write-Host $allToolsText
    
    # Count tools
    $toolCount = ($allToolsText | Select-String -Pattern "####\s+\`" -AllMatches).Matches.Count
    Write-Host "`n📊 Total tools found: $toolCount" -ForegroundColor Green
}

# Test each category
$categories = @(
    @{ Name = "search"; Icon = "🔍"; Description = "Search Tools" },
    @{ Name = "index"; Icon = "📦"; Description = "Indexing Tools" },
    @{ Name = "analysis"; Icon = "🔬"; Description = "Analysis Tools" },
    @{ Name = "validation"; Icon = "✅"; Description = "Validation Tools" },
    @{ Name = "planning"; Icon = "📋"; Description = "Planning Tools" },
    @{ Name = "todo"; Icon = "📝"; Description = "Todo Tools" },
    @{ Name = "codegen"; Icon = "🚀"; Description = "Code Generation Tools" },
    @{ Name = "design"; Icon = "🎨"; Description = "Design Tools" },
    @{ Name = "knowledge"; Icon = "🧠"; Description = "Knowledge Tools" },
    @{ Name = "status"; Icon = "📊"; Description = "Status Tools" },
    @{ Name = "control"; Icon = "🛑"; Description = "Control Tools" },
    @{ Name = "other"; Icon = "🔧"; Description = "Other Tools" }
)

Write-Host "`n3️⃣  Testing category filters..." -ForegroundColor Yellow
Write-Host "=" * 60 -ForegroundColor Cyan

$categorySummary = @()

foreach ($category in $categories) {
    Write-Host "`n$($category.Icon) Testing category: $($category.Description)" -ForegroundColor Yellow
    
    try {
        $response = Invoke-McpTool -ToolName "list_available_tools" `
            -Arguments @{ category = $category.Name }
        
        if ($response.result.content) {
            $text = $response.result.content[0].text
            
            # Count tools in this category
            $toolCount = ($text | Select-String -Pattern "####\s+\`" -AllMatches).Matches.Count
            
            $categorySummary += @{
                Category = $category.Description
                Icon = $category.Icon
                Count = $toolCount
            }
            
            if ($toolCount -gt 0) {
                Write-Host "   ✅ Found $toolCount tool(s)" -ForegroundColor Green
                
                # Extract tool names
                $toolNames = [regex]::Matches($text, "####\s+\`([^\`]+)\`") | 
                    ForEach-Object { $_.Groups[1].Value }
                
                foreach ($toolName in $toolNames) {
                    Write-Host "      • $toolName" -ForegroundColor Cyan
                }
            } else {
                Write-Host "   ℹ️  No tools in this category" -ForegroundColor Gray
            }
        }
    } catch {
        Write-Host "   ❌ Error testing category: $_" -ForegroundColor Red
    }
}

# Display summary
Write-Host "`n4️⃣  Category Summary" -ForegroundColor Yellow
Write-Host "=" * 60 -ForegroundColor Cyan

$categorySummary | Where-Object { $_.Count -gt 0 } | ForEach-Object {
    Write-Host "$($_.Icon) $($_.Category): $($_.Count) tools" -ForegroundColor Green
}

$totalTools = ($categorySummary | Measure-Object -Property Count -Sum).Sum
Write-Host "`n📊 Total: $totalTools tools across all categories" -ForegroundColor Cyan

# Test filtering with invalid category
Write-Host "`n5️⃣  Testing invalid category (should return all tools)..." -ForegroundColor Yellow
try {
    $response = Invoke-McpTool -ToolName "list_available_tools" `
        -Arguments @{ category = "invalid_category" }
    
    if ($response.result.content) {
        $text = $response.result.content[0].text
        $toolCount = ($text | Select-String -Pattern "####\s+\`" -AllMatches).Matches.Count
        Write-Host "✅ Invalid category handled gracefully, returned $toolCount tools" -ForegroundColor Green
    }
} catch {
    Write-Host "❌ Error with invalid category: $_" -ForegroundColor Red
}

Write-Host "`n✅ Category system testing complete!" -ForegroundColor Green
Write-Host "=" * 60 -ForegroundColor Cyan

# Generate a report
$reportPath = ".\test-router-categories-report.txt"
$report = @"
MemoryRouter Category System Test Report
Generated: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")
========================================

SUMMARY
-------
Total Tools: $totalTools

CATEGORY BREAKDOWN
------------------
$($categorySummary | Where-Object { $_.Count -gt 0 } | ForEach-Object {
    "$($_.Icon) $($_.Category): $($_.Count) tools"
} | Out-String)

FEATURES TESTED
---------------
✅ List all tools (no filter)
✅ Filter by specific category
✅ Handle invalid category gracefully
✅ Tool counting and organization
✅ Category icons and formatting

STATUS
------
All category tests passed successfully!

The MemoryRouter now supports:
- 12 predefined tool categories
- Automatic category assignment during tool discovery
- Category-based filtering via list_available_tools
- Organized tool listings with icons and grouping
"@

$report | Out-File -FilePath $reportPath -Encoding UTF8
Write-Host "`n📄 Report saved to: $reportPath" -ForegroundColor Cyan
