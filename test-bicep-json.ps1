#!/usr/bin/env pwsh
<#
.SYNOPSIS
Test Bicep and JSON parser functionality

.DESCRIPTION
Tests the new Bicep and JSON file parsers with exclusions
#>

$ServerUrl = "http://localhost:5098"
$ErrorActionPreference = "Continue"

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "Bicep & JSON Parser Test" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

# Wait for server
Start-Sleep -Seconds 5

# Test 1: Create and index a Bicep file
Write-Host "[1] Testing Bicep File Parser..." -ForegroundColor Yellow
try {
    $bicepContent = @"
param location string = 'eastus'
param storageAccountName string

var uniqueName = '\${storageAccountName}-\${uniqueString(resourceGroup().id)}'

resource storageAccount 'Microsoft.Storage/storageAccounts@2021-02-01' = {
  name: uniqueName
  location: location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
}

module appService './modules/appService.bicep' = {
  name: 'appServiceDeployment'
  params: {
    location: location
  }
}

output storageAccountId string = storageAccount.id
"@
    
    docker exec cbcai-agent-server sh -c "echo '$bicepContent' > /tmp/test.bicep"
    
    $indexBody = @{
        path = "/tmp/test.bicep"
        context = "test"
    } | ConvertTo-Json

    $result = Invoke-RestMethod -Uri "$ServerUrl/api/index/file" -Method POST -Body $indexBody -ContentType 'application/json' -TimeoutSec 30
    
    if ($result.success) {
        Write-Host "  ✓ Bicep file indexed successfully!" -ForegroundColor Green
        Write-Host "    - Files: $($result.filesIndexed)" -ForegroundColor Gray
        Write-Host "    - Patterns: $($result.patternsDetected)" -ForegroundColor Gray
    } else {
        Write-Host "  ✗ Failed to index Bicep file" -ForegroundColor Red
        Write-Host "    Errors: $($result.errors -join ', ')" -ForegroundColor Red
    }
}
catch {
    Write-Host "  ✗ Bicep test failed: $($_.Exception.Message)" -ForegroundColor Red
}

# Test 2: Index a regular JSON file (should work)
Write-Host "`n[2] Testing JSON File Parser (non-config)..." -ForegroundColor Yellow
try {
    $jsonContent = @"
{
  "database": {
    "host": "localhost",
    "port": 5432,
    "credentials": {
      "username": "admin",
      "password": "secret"
    }
  },
  "features": [
    {
      "name": "feature1",
      "enabled": true
    },
    {
      "name": "feature2",
      "enabled": false
    }
  ],
  "apiEndpoints": {
    "users": "/api/users",
    "products": "/api/products"
  }
}
"@
    
    docker exec cbcai-agent-server sh -c "echo '$jsonContent' > /tmp/settings.json"
    
    $indexBody = @{
        path = "/tmp/settings.json"
        context = "test"
    } | ConvertTo-Json

    $result = Invoke-RestMethod -Uri "$ServerUrl/api/index/file" -Method POST -Body $indexBody -ContentType 'application/json' -TimeoutSec 30
    
    if ($result.success) {
        Write-Host "  ✓ JSON file indexed successfully!" -ForegroundColor Green
        Write-Host "    - Files: $($result.filesIndexed)" -ForegroundColor Gray
        Write-Host "    - Patterns: $($result.patternsDetected)" -ForegroundColor Gray
    } else {
        Write-Host "  ✗ Failed to index JSON file" -ForegroundColor Red
        Write-Host "    Errors: $($result.errors -join ', ')" -ForegroundColor Red
    }
}
catch {
    Write-Host "  ✗ JSON test failed: $($_.Exception.Message)" -ForegroundColor Red
}

# Test 3: Try to index appsettings.json (should be excluded)
Write-Host "`n[3] Testing JSON Exclusion (appsettings.json)..." -ForegroundColor Yellow
try {
    $configContent = @"
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=test"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  }
}
"@
    
    docker exec cbcai-agent-server sh -c "echo '$configContent' > /tmp/appsettings.json"
    
    $indexBody = @{
        path = "/tmp/appsettings.json"
        context = "test"
    } | ConvertTo-Json

    $result = Invoke-RestMethod -Uri "$ServerUrl/api/index/file" -Method POST -Body $indexBody -ContentType 'application/json' -TimeoutSec 30
    
    if ($result.success -eq $false -and $result.errors -like "*Excluded*") {
        Write-Host "  ✓ appsettings.json correctly excluded!" -ForegroundColor Green
        Write-Host "    - Message: $($result.errors[0])" -ForegroundColor Gray
    } else {
        Write-Host "  ⚠ appsettings.json was NOT excluded (expected to be excluded)" -ForegroundColor Yellow
    }
}
catch {
    Write-Host "  ✗ Exclusion test failed: $($_.Exception.Message)" -ForegroundColor Red
}

# Test 4: Query for indexed data
Write-Host "`n[4] Querying indexed Bicep/JSON data..." -ForegroundColor Yellow
try {
    $queryBody = @{
        query = "Azure storage account configuration"
        context = "test"
        limit = 5
    } | ConvertTo-Json

    $queryResult = Invoke-RestMethod -Uri "$ServerUrl/api/query" -Method POST -Body $queryBody -ContentType 'application/json' -TimeoutSec 30
    
    Write-Host "  ✓ Query successful! Found $($queryResult.results.Count) result(s)" -ForegroundColor Green
    
    if ($queryResult.results.Count -gt 0) {
        Write-Host "`n  Top Results:" -ForegroundColor Gray
        $queryResult.results | Select-Object -First 3 | ForEach-Object {
            Write-Host "    - $($_.name) (Score: $($_.score.ToString('0.00')))" -ForegroundColor Gray
            Write-Host "      Type: $($_.type), File: $($_.filePath)" -ForegroundColor DarkGray
        }
    }
}
catch {
    Write-Host "  ✗ Query failed: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "✅ Tests Complete!" -ForegroundColor Green
Write-Host "========================================`n" -ForegroundColor Cyan

# Show what's now supported
Write-Host "Supported File Types:" -ForegroundColor Cyan
Write-Host "  ✓ .cs, .vb (C#/VB.NET)" -ForegroundColor White
Write-Host "  ✓ .cshtml, .razor (Razor)" -ForegroundColor White
Write-Host "  ✓ .py (Python)" -ForegroundColor White
Write-Host "  ✓ .md, .markdown (Markdown)" -ForegroundColor White
Write-Host "  ✓ .css, .scss, .less (Stylesheets)" -ForegroundColor White
Write-Host "  ✓ .bicep (Azure Bicep) - NEW!" -ForegroundColor Green
Write-Host "  ✓ .json (JSON, with smart exclusions) - NEW!" -ForegroundColor Green
Write-Host ""

