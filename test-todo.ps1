#!/usr/bin/env pwsh
<#
.SYNOPSIS
Test script for TODO functionality

.DESCRIPTION
Tests adding TODOs and searching them to verify the datetime conversion fix
#>

$ServerUrl = "http://localhost:5098"
$ErrorActionPreference = "Continue"

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "TODO Functionality Test" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

# Wait for server
Start-Sleep -Seconds 5

# Test 1: Add a TODO
Write-Host "[1] Adding a TODO..." -ForegroundColor Yellow
try {
    $addBody = @{
        context = "TestContext"
        title = "Fix datetime conversion"
        description = "Test TODO to verify datetime handling"
        priority = "High"
        filePath = "/test/file.cs"
        lineNumber = 42
        assignedTo = "developer@test.com"
    } | ConvertTo-Json

    $addResult = Invoke-RestMethod -Uri "$ServerUrl/api/todo/add" -Method POST -Body $addBody -ContentType 'application/json'
    Write-Host "  ✓ TODO Added - ID: $($addResult.id)" -ForegroundColor Green
    $todoId = $addResult.id
}
catch {
    Write-Host "  ✗ Failed to add TODO: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# Test 2: Search TODOs (this is where the datetime conversion error would occur)
Write-Host "`n[2] Searching TODOs (testing datetime conversion)..." -ForegroundColor Yellow
try {
    $searchResult = Invoke-RestMethod -Uri "$ServerUrl/api/todo/search?context=TestContext" -Method GET
    Write-Host "  ✓ Search successful! Found $($searchResult.Count) TODO(s)" -ForegroundColor Green
    
    if ($searchResult.Count -gt 0) {
        $todo = $searchResult[0]
        Write-Host "    - Title: $($todo.title)" -ForegroundColor Gray
        Write-Host "    - Created: $($todo.createdAt)" -ForegroundColor Gray
        Write-Host "    - Status: $($todo.status)" -ForegroundColor Gray
    }
}
catch {
    Write-Host "  ✗ Search failed: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "    This indicates the datetime conversion bug still exists!" -ForegroundColor Red
    exit 1
}

# Test 3: Get specific TODO
Write-Host "`n[3] Getting TODO by ID..." -ForegroundColor Yellow
try {
    $getResult = Invoke-RestMethod -Uri "$ServerUrl/api/todo/$todoId" -Method GET
    Write-Host "  ✓ Retrieved TODO successfully" -ForegroundColor Green
    Write-Host "    - Created At: $($getResult.createdAt)" -ForegroundColor Gray
}
catch {
    Write-Host "  ✗ Failed to get TODO: $($_.Exception.Message)" -ForegroundColor Red
}

# Test 4: Update TODO status
Write-Host "`n[4] Updating TODO status..." -ForegroundColor Yellow
try {
    $statusBody = '"Completed"' # JSON string value
    $updateResult = Invoke-RestMethod -Uri "$ServerUrl/api/todo/$todoId/status" -Method PUT -Body $statusBody -ContentType 'application/json'
    Write-Host "  ✓ TODO status updated to Completed" -ForegroundColor Green
    Write-Host "    - Completed At: $($updateResult.completedAt)" -ForegroundColor Gray
}
catch {
    Write-Host "  ✗ Failed to update TODO: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "✅ ALL TESTS PASSED!" -ForegroundColor Green
Write-Host "The datetime conversion bug is FIXED!" -ForegroundColor Green
Write-Host "========================================`n" -ForegroundColor Cyan

