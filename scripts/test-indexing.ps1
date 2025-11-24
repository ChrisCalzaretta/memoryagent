#!/usr/bin/env pwsh
<#
.SYNOPSIS
Test script to identify where the indexing is crashing

.DESCRIPTION
Systematically tests each component to find the crash point
#>

param(
    [string]$ServerUrl = "http://localhost:5098"
)

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "Indexing Crash Diagnostic Test" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

$ErrorActionPreference = "Continue"
$testResults = @()

function Test-Endpoint {
    param(
        [string]$Name,
        [string]$Url,
        [string]$Method = "GET",
        [object]$Body = $null,
        [int]$TimeoutSec = 30
    )
    
    Write-Host "`n[$Name]" -ForegroundColor Yellow
    Write-Host "  URL: $Url" -ForegroundColor Gray
    
    try {
        $params = @{
            Uri = $Url
            Method = $Method
            TimeoutSec = $TimeoutSec
            ErrorAction = "Stop"
        }
        
        if ($Body) {
            $params.Body = ($Body | ConvertTo-Json -Depth 10)
            $params.ContentType = "application/json"
            Write-Host "  Body: $($params.Body)" -ForegroundColor Gray
        }
        
        $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
        $response = Invoke-RestMethod @params
        $stopwatch.Stop()
        
        Write-Host "  ✓ SUCCESS ($($stopwatch.ElapsedMilliseconds)ms)" -ForegroundColor Green
        
        $script:testResults += [PSCustomObject]@{
            Test = $Name
            Status = "PASS"
            Duration = $stopwatch.ElapsedMilliseconds
            Error = $null
        }
        
        return $response
    }
    catch {
        Write-Host "  ✗ FAILED: $($_.Exception.Message)" -ForegroundColor Red
        
        $script:testResults += [PSCustomObject]@{
            Test = $Name
            Status = "FAIL"
            Duration = 0
            Error = $_.Exception.Message
        }
        
        # Check if server is still running
        Start-Sleep -Seconds 2
        try {
            $healthCheck = Invoke-RestMethod -Uri "$ServerUrl/api/health" -TimeoutSec 5 -ErrorAction SilentlyContinue
            Write-Host "  Server still responding to health checks" -ForegroundColor Yellow
        }
        catch {
            Write-Host "  ⚠ SERVER CRASHED - Not responding to health checks!" -ForegroundColor Red
        }
        
        return $null
    }
}

# Test 1: Health Check
Write-Host "`n=== Phase 1: Basic Health Checks ===" -ForegroundColor Cyan
Test-Endpoint -Name "Health Check" -Url "$ServerUrl/api/health"

# Test 2: Check Qdrant directly
Write-Host "`n=== Phase 2: Database Health ===" -ForegroundColor Cyan
Test-Endpoint -Name "Qdrant Collections" -Url "http://localhost:6431/collections"

# Test 3: Simple MCP endpoint
Write-Host "`n=== Phase 3: MCP Protocol ===" -ForegroundColor Cyan
$mcpBody = @{
    jsonrpc = "2.0"
    id = 1
    method = "initialize"
    params = @{
        protocolVersion = "2024-11-05"
        capabilities = @{}
        clientInfo = @{
            name = "test-client"
            version = "1.0.0"
        }
    }
}
Test-Endpoint -Name "MCP Initialize" -Url "$ServerUrl/mcp" -Method "POST" -Body $mcpBody

# Test 4: Create a minimal test file
Write-Host "`n=== Phase 4: File System Check ===" -ForegroundColor Cyan
docker exec cbcai-agent-server sh -c "echo 'public class TestClass { }' > /tmp/test.cs"
Write-Host "  Created test file: /tmp/test.cs" -ForegroundColor Gray

# Test 5: Index the simple test file
Write-Host "`n=== Phase 5: Simple File Index ===" -ForegroundColor Cyan
$indexBody = @{
    path = "/tmp/test.cs"
    context = "test"
}
Test-Endpoint -Name "Index Simple CS File" -Url "$ServerUrl/api/index/file" -Method "POST" -Body $indexBody -TimeoutSec 60

# Test 6: Index a markdown file
Write-Host "`n=== Phase 6: Markdown File Index ===" -ForegroundColor Cyan
docker exec cbcai-agent-server sh -c "echo '# Test Markdown' > /tmp/test.md"
$indexBody = @{
    path = "/tmp/test.md"
    context = "test"
}
Test-Endpoint -Name "Index Markdown File" -Url "$ServerUrl/api/index/file" -Method "POST" -Body $indexBody -TimeoutSec 60

# Test 7: Index a CSS file
Write-Host "`n=== Phase 7: CSS File Index ===" -ForegroundColor Cyan
docker exec cbcai-agent-server sh -c "echo '.test { color: red; }' > /tmp/test.css"
$indexBody = @{
    path = "/tmp/test.css"
    context = "test"
}
Test-Endpoint -Name "Index CSS File" -Url "$ServerUrl/api/index/file" -Method "POST" -Body $indexBody -TimeoutSec 60

# Test 8: Index a Razor file
Write-Host "`n=== Phase 8: Razor File Index ===" -ForegroundColor Cyan
docker exec cbcai-agent-server sh -c "echo '@page' > /tmp/test.cshtml"
$indexBody = @{
    path = "/tmp/test.cshtml"
    context = "test"
}
Test-Endpoint -Name "Index Razor File" -Url "$ServerUrl/api/index/file" -Method "POST" -Body $indexBody -TimeoutSec 60

# Test 9: Index a real file from CBC_AI
Write-Host "`n=== Phase 9: Real Project File ===" -ForegroundColor Cyan
$indexBody = @{
    path = "/workspace/CBC_AI/LicenseServer/LicenseServer.API/Controllers/LandingController.cs"
    context = "CBC_AI"
}
Test-Endpoint -Name "Index Real CS File" -Url "$ServerUrl/api/index/file" -Method "POST" -Body $indexBody -TimeoutSec 120

# Test 10: Query
Write-Host "`n=== Phase 10: Query Test ===" -ForegroundColor Cyan
$queryBody = @{
    query = "test"
    context = "test"
    limit = 5
    minimumScore = 0.5
}
Test-Endpoint -Name "Query Indexed Data" -Url "$ServerUrl/api/query" -Method "POST" -Body $queryBody -TimeoutSec 30

# Print Summary
Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "Test Summary" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

$testResults | Format-Table -AutoSize

$failedTests = $testResults | Where-Object { $_.Status -eq "FAIL" }
$passedTests = $testResults | Where-Object { $_.Status -eq "PASS" }

Write-Host "`nTotal Tests: $($testResults.Count)" -ForegroundColor White
Write-Host "Passed: $($passedTests.Count)" -ForegroundColor Green
Write-Host "Failed: $($failedTests.Count)" -ForegroundColor Red

if ($failedTests.Count -gt 0) {
    Write-Host "`n=== Failed Tests ===" -ForegroundColor Red
    $failedTests | ForEach-Object {
        Write-Host "  • $($_.Test): $($_.Error)" -ForegroundColor Red
    }
    
    # Find first failure
    $firstFailure = $testResults | Where-Object { $_.Status -eq "FAIL" } | Select-Object -First 1
    Write-Host "`n🔍 FIRST FAILURE: $($firstFailure.Test)" -ForegroundColor Yellow
    Write-Host "   This is likely where the crash occurs!" -ForegroundColor Yellow
}

# Check server logs
Write-Host "`n=== Recent Server Logs ===" -ForegroundColor Cyan
docker logs cbcai-agent-server --tail 50 2>&1 | Select-Object -Last 30

Write-Host "`n=== Container Status ===" -ForegroundColor Cyan
docker ps -a --filter "name=cbcai-agent-server" --format "table {{.Names}}\t{{.Status}}\t{{.CreatedAt}}"

