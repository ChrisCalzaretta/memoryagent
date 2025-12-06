# Memory Agent - Stop Shared Stack (Graceful Shutdown)
# CRITICAL: This script ensures proper shutdown order to prevent database corruption

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Memory Agent - Graceful Shutdown" -ForegroundColor Yellow
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Step 1: Stop MCP server FIRST (closes Neo4j/Qdrant connections)
Write-Host "Step 1/3: Stopping MCP server (closing database connections)..." -ForegroundColor Yellow
$mcpResult = docker stop --time 30 memory-agent-server 2>&1
if ($LASTEXITCODE -eq 0) {
    Write-Host "  ✅ MCP server stopped gracefully" -ForegroundColor Green
} else {
    Write-Host "  ⚠️ MCP server not running or already stopped" -ForegroundColor Yellow
}

# Wait a moment for connections to fully close
Write-Host "  Waiting 5 seconds for connections to close..." -ForegroundColor Gray
Start-Sleep -Seconds 5

# Step 2: Stop Qdrant (faster shutdown than Neo4j)
Write-Host "Step 2/3: Stopping Qdrant..." -ForegroundColor Yellow
$qdrantResult = docker stop --time 60 memory-agent-qdrant 2>&1
if ($LASTEXITCODE -eq 0) {
    Write-Host "  ✅ Qdrant stopped gracefully" -ForegroundColor Green
} else {
    Write-Host "  ⚠️ Qdrant not running or already stopped" -ForegroundColor Yellow
}

# Step 3: Stop Neo4j LAST (needs most time for transaction log flush)
Write-Host "Step 3/3: Stopping Neo4j (flushing transaction logs)..." -ForegroundColor Yellow
$neo4jResult = docker stop --time 120 memory-agent-neo4j 2>&1
if ($LASTEXITCODE -eq 0) {
    Write-Host "  ✅ Neo4j stopped gracefully" -ForegroundColor Green
} else {
    Write-Host "  ⚠️ Neo4j not running or already stopped" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "✅ Graceful shutdown complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Data preserved in: d:\Memory\shared\" -ForegroundColor Yellow
Write-Host ""
Write-Host "To restart: .\start-shared-stack.ps1" -ForegroundColor Cyan
Write-Host ""
