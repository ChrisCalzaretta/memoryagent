#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Install Cursor Job Status Extension

.DESCRIPTION
    Copies the job-status extension to Cursor's extensions directory
    and reloads Cursor to activate it.

.EXAMPLE
    .\install-job-status-extension.ps1
#>

$ErrorActionPreference = "Stop"

Write-Host "🚀 Installing Cursor Job Status Extension..." -ForegroundColor Cyan
Write-Host ""

# Paths
$sourceDir = Join-Path $PSScriptRoot ".cursor-extensions\job-status"
$targetDir = Join-Path $env:USERPROFILE ".cursor\extensions\cursor-job-status-1.0.0"

# Verify source exists
if (-not (Test-Path $sourceDir)) {
    Write-Host "❌ Source directory not found: $sourceDir" -ForegroundColor Red
    Write-Host "   Make sure you're running this from the MemoryAgent root directory" -ForegroundColor Yellow
    exit 1
}

Write-Host "📁 Source: $sourceDir" -ForegroundColor Gray
Write-Host "📁 Target: $targetDir" -ForegroundColor Gray
Write-Host ""

# Check if already installed
if (Test-Path $targetDir) {
    Write-Host "⚠️  Extension already installed. Updating..." -ForegroundColor Yellow
    Remove-Item -Path $targetDir -Recurse -Force
}

# Create target directory
Write-Host "📦 Copying extension files..." -ForegroundColor Cyan
New-Item -Path $targetDir -ItemType Directory -Force | Out-Null

# Copy files
Copy-Item -Path "$sourceDir\*" -Destination $targetDir -Recurse -Force

# Verify installation
$installedFiles = Get-ChildItem -Path $targetDir -File
Write-Host ""
Write-Host "✅ Extension installed successfully!" -ForegroundColor Green
Write-Host ""
Write-Host "📄 Installed files:" -ForegroundColor Gray
foreach ($file in $installedFiles) {
    Write-Host "   - $($file.Name)" -ForegroundColor Gray
}

Write-Host ""
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Gray
Write-Host ""
Write-Host "🎯 Next Steps:" -ForegroundColor Cyan
Write-Host ""
Write-Host "1. Restart Cursor to activate the extension:" -ForegroundColor White
Write-Host "   - Close all Cursor windows" -ForegroundColor Gray
Write-Host "   - Reopen Cursor" -ForegroundColor Gray
Write-Host ""
Write-Host "   OR press Ctrl+Shift+P and run:" -ForegroundColor White
Write-Host "   'Developer: Reload Window'" -ForegroundColor Yellow
Write-Host ""
Write-Host "2. Make sure your services are running:" -ForegroundColor White
Write-Host "   - CodingOrchestrator: http://localhost:5001" -ForegroundColor Gray
Write-Host "   - MemoryRouter: http://localhost:5010" -ForegroundColor Gray
Write-Host ""
Write-Host "3. Look for the status bar at the bottom-left:" -ForegroundColor White
Write-Host "   💤 No active jobs" -ForegroundColor Gray
Write-Host ""
Write-Host "4. Start a job and watch it update in real-time!" -ForegroundColor White
Write-Host ""
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Gray
Write-Host ""
Write-Host "💡 Tips:" -ForegroundColor Cyan
Write-Host "   - Click the status bar to see job details" -ForegroundColor Gray
Write-Host "   - Configure in Settings → Extensions → Job Status" -ForegroundColor Gray
Write-Host "   - Check 'View → Output → Cursor Job Status' for logs" -ForegroundColor Gray
Write-Host ""
Write-Host "📚 Documentation: .cursor-extensions/job-status/README.md" -ForegroundColor Gray
Write-Host ""



