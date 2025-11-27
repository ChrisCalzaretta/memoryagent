# Memory Agent - Start Shared Stack
# All projects will connect to this single stack (port 5000)

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Memory Agent - Shared Stack Startup" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Check if stack is already running
$running = docker ps --filter "name=memory-agent-server" --format "{{.Names}}"
if ($running -eq "memory-agent-server") {
    Write-Host "✅ Shared stack is already running" -ForegroundColor Green
    Write-Host "   Port: 5000" -ForegroundColor Yellow
    exit 0
}

Write-Host "Starting shared MCP stack..." -ForegroundColor Yellow

# Start the stack
docker-compose -f docker-compose-shared.yml up -d

if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Failed to start shared stack" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "✅ Shared MCP Stack Started!" -ForegroundColor Green
Write-Host ""
Write-Host "Services:" -ForegroundColor Cyan
Write-Host "  • MCP Server:  http://localhost:5000" -ForegroundColor White
Write-Host "  • Qdrant:      http://localhost:6333" -ForegroundColor White
Write-Host "  • Neo4j:       http://localhost:7474" -ForegroundColor White
Write-Host "  • Ollama:      http://localhost:11434" -ForegroundColor White
Write-Host ""
Write-Host "Storage:" -ForegroundColor Cyan
Write-Host "  • Data: d:\Memory\shared\" -ForegroundColor White
Write-Host ""
Write-Host "Workspace Mount:" -ForegroundColor Cyan
Write-Host "  • E:\GitHub → /workspace (in container)" -ForegroundColor White
Write-Host ""
Write-Host "Next Steps:" -ForegroundColor Cyan
Write-Host "  1. Configure Cursor MCP settings" -ForegroundColor White
Write-Host "  2. Open any project in E:\GitHub in Cursor" -ForegroundColor White
Write-Host "  3. File watcher will auto-register and monitor changes" -ForegroundColor White
Write-Host ""

