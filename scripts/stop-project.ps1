# Stop Memory Code Agent for a specific project
# Usage: .\stop-project.ps1 -ProjectName "cbc"

param(
    [Parameter(Mandatory=$false, HelpMessage="Project name (defaults to last started project)")]
    [string]$ProjectName
)

$ErrorActionPreference = "Stop"

# If no project name provided, try to get from last-project.json
if (-not $ProjectName) {
    if (Test-Path ".\last-project.json") {
        $config = Get-Content ".\last-project.json" | ConvertFrom-Json
        $ProjectName = $config.ProjectName
        Write-Host "Using last project: $ProjectName" -ForegroundColor Yellow
    } else {
        Write-Host "Error: No project name provided and no last project found" -ForegroundColor Red
        Write-Host "Usage: .\stop-project.ps1 -ProjectName 'cbc'" -ForegroundColor Yellow
        exit 1
    }
}

$ProjectName = $ProjectName.ToLower() -replace '[^a-z0-9]', ''

Write-Host "Stopping Memory Code Agent for project: $ProjectName" -ForegroundColor Cyan

# Set environment variable for docker-compose
$env:PROJECT_NAME = $ProjectName

# Stop containers
docker-compose down

if ($LASTEXITCODE -eq 0) {
    Write-Host "Successfully stopped containers for project: $ProjectName" -ForegroundColor Green
} else {
    Write-Host "Error stopping containers" -ForegroundColor Red
    exit 1
}

