#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Monitor a specific job in real-time
#>

param(
    [string]$JobId = "job_20251221214714_164d9f6369574c50ae23324c693c647f",
    [int]$RefreshSeconds = 3
)

$ErrorActionPreference = "Stop"

function Get-JobStatus {
    param([string]$Id)
    
    try {
        $response = Invoke-RestMethod -Uri "http://localhost:5001/api/orchestrator/status/$Id" -Method Get -TimeoutSec 5
        return $response
    } catch {
        return $null
    }
}

function Show-JobStatus {
    param($Job)
    
    if (-not $Job) {
        Write-Host "❌ Job not found or server not responding" -ForegroundColor Red
        return $false
    }
    
    Clear-Host
    
    Write-Host ""
    Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
    Write-Host "  🎯 JOB MONITOR" -ForegroundColor Cyan
    Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
    Write-Host ""
    
    # Status icon
    $icon = switch ($Job.status) {
        { $_ -match "running" } { "🔄" }
        "completed" { "✅" }
        "failed" { "❌" }
        "cancelled" { "🚫" }
        default { "❓" }
    }
    
    # Task (truncate if too long)
    $task = $Job.task
    if ($task.Length -gt 80) {
        $task = $task.Substring(0, 77) + "..."
    }
    
    Write-Host "Task: " -NoNewline -ForegroundColor Gray
    Write-Host $task -ForegroundColor White
    Write-Host ""
    
    Write-Host "Job ID: " -NoNewline -ForegroundColor Gray
    Write-Host $Job.jobId -ForegroundColor Yellow
    Write-Host ""
    
    Write-Host "Status: " -NoNewline -ForegroundColor Gray
    Write-Host "$icon $($Job.status)" -ForegroundColor $(
        if ($Job.status -match "completed") { "Green" }
        elseif ($Job.status -match "failed") { "Red" }
        elseif ($Job.status -match "running") { "Cyan" }
        else { "Yellow" }
    )
    Write-Host ""
    
    # Progress bar
    $progress = [math]::Max(0, [math]::Min(100, $Job.progress))
    $barLength = 40
    $filled = [math]::Floor($barLength * $progress / 100)
    $empty = $barLength - $filled
    
    $bar = "█" * $filled + "░" * $empty
    
    Write-Host "Progress: " -NoNewline -ForegroundColor Gray
    Write-Host "[$bar] " -NoNewline -ForegroundColor Cyan
    Write-Host "$progress%" -ForegroundColor White
    Write-Host ""
    
    # Calculate duration
    $startedAt = [DateTime]::Parse($Job.startedAt)
    $duration = (Get-Date) - $startedAt
    $durationStr = if ($duration.TotalHours -ge 1) {
        "{0:h\h\ m\m}" -f $duration
    } elseif ($duration.TotalMinutes -ge 1) {
        "{0:m\m\ s\s}" -f $duration
    } else {
        "{0:s\s}" -f $duration
    }
    
    Write-Host "Duration: " -NoNewline -ForegroundColor Gray
    Write-Host $durationStr -ForegroundColor White
    Write-Host ""
    
    Write-Host "Started: " -NoNewline -ForegroundColor Gray
    Write-Host $startedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") -ForegroundColor White
    Write-Host ""
    
    if ($Job.completedAt) {
        $completedAt = [DateTime]::Parse($Job.completedAt)
        Write-Host "Completed: " -NoNewline -ForegroundColor Gray
        Write-Host $completedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") -ForegroundColor White
        Write-Host ""
    }
    
    if ($Job.error) {
        Write-Host ""
        Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Red
        Write-Host "  ❌ ERROR" -ForegroundColor Red
        Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Red
        Write-Host ""
        Write-Host $Job.error -ForegroundColor Red
        Write-Host ""
    }
    
    if ($Job.result -and $Job.result.summary) {
        Write-Host ""
        Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Green
        Write-Host "  ✅ RESULT" -ForegroundColor Green
        Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Green
        Write-Host ""
        Write-Host $Job.result.summary -ForegroundColor White
        Write-Host ""
        
        if ($Job.result.files -and $Job.result.files.Count -gt 0) {
            Write-Host "Files Generated: $($Job.result.files.Count)" -ForegroundColor Cyan
            foreach ($file in $Job.result.files) {
                Write-Host "  📄 $($file.path)" -ForegroundColor Gray
            }
            Write-Host ""
        }
    }
    
    Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
    Write-Host ""
    
    $isRunning = $Job.status -match "running"
    
    if ($isRunning) {
        Write-Host "⏱️  Refreshing in $RefreshSeconds seconds... (Press Ctrl+C to stop)" -ForegroundColor Gray
    } else {
        Write-Host "✅ Job completed - monitoring stopped" -ForegroundColor Green
    }
    
    Write-Host ""
    
    return $isRunning
}

# Main monitoring loop
Write-Host ""
Write-Host "🎯 Starting job monitor for: $JobId" -ForegroundColor Cyan
Write-Host "   Refresh interval: $RefreshSeconds seconds" -ForegroundColor Gray
Write-Host ""
Write-Host "Press Ctrl+C to stop monitoring" -ForegroundColor Yellow
Start-Sleep -Seconds 2

try {
    while ($true) {
        $job = Get-JobStatus -Id $JobId
        $isRunning = Show-JobStatus -Job $job
        
        if (-not $isRunning) {
            break
        }
        
        Start-Sleep -Seconds $RefreshSeconds
    }
} catch {
    Write-Host ""
    Write-Host "❌ Error: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host ""
}

Write-Host ""
Write-Host "Monitor stopped." -ForegroundColor Gray
Write-Host ""


