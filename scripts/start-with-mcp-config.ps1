# Start Docker services with Anthropic config from mcp.json
# Reads ANTHROPIC_API_KEY, ANTHROPIC_MODEL, ANTHROPIC_MODEL_PREMIUM from Cursor's mcp.json

param(
    [string]$ComposeFile = "docker-compose-shared-Calzaretta.yml",
    [string]$Service = "",  # Empty = all services, or specify like "coding-agent"
    [switch]$Build,
    [switch]$Rebuild
)

$McpJsonPath = "$env:USERPROFILE\.cursor\mcp.json"

Write-Host "🔧 Loading Anthropic config from mcp.json..." -ForegroundColor Cyan

if (Test-Path $McpJsonPath) {
    try {
        $mcpConfig = Get-Content $McpJsonPath -Raw | ConvertFrom-Json
        
        # Try to get from coding-orchestrator env (where user put it)
        $orchestratorEnv = $mcpConfig.mcpServers.'coding-orchestrator'.env
        
        if ($orchestratorEnv) {
            if ($orchestratorEnv.ANTHROPIC_API_KEY) {
                $env:ANTHROPIC_API_KEY = $orchestratorEnv.ANTHROPIC_API_KEY
                Write-Host "  ✅ ANTHROPIC_API_KEY loaded" -ForegroundColor Green
            }
            if ($orchestratorEnv.ANTHROPIC_MODEL) {
                $env:ANTHROPIC_MODEL = $orchestratorEnv.ANTHROPIC_MODEL
                Write-Host "  ✅ ANTHROPIC_MODEL: $($orchestratorEnv.ANTHROPIC_MODEL)" -ForegroundColor Green
            }
            if ($orchestratorEnv.ANTHROPIC_MODEL_PREMIUM) {
                $env:ANTHROPIC_MODEL_PREMIUM = $orchestratorEnv.ANTHROPIC_MODEL_PREMIUM
                Write-Host "  ✅ ANTHROPIC_MODEL_PREMIUM: $($orchestratorEnv.ANTHROPIC_MODEL_PREMIUM)" -ForegroundColor Green
            }
        }
        else {
            Write-Host "  ⚠️ No Anthropic config found in mcp.json (coding-orchestrator.env)" -ForegroundColor Yellow
        }
    }
    catch {
        Write-Host "  ❌ Error parsing mcp.json: $_" -ForegroundColor Red
    }
}
else {
    Write-Host "  ⚠️ mcp.json not found at $McpJsonPath" -ForegroundColor Yellow
}

# Build the docker-compose command
$composeCmd = "docker-compose -f $ComposeFile"

if ($Rebuild) {
    $composeCmd += " build --no-cache"
    if ($Service) { $composeCmd += " $Service" }
    Write-Host "`n🔨 Rebuilding with no cache..." -ForegroundColor Cyan
    Invoke-Expression $composeCmd
    $composeCmd = "docker-compose -f $ComposeFile"
}

if ($Build) {
    $composeCmd += " up -d --build"
}
else {
    $composeCmd += " up -d"
}

if ($Service) {
    $composeCmd += " $Service"
}

Write-Host "`n🚀 Starting Docker services..." -ForegroundColor Cyan
Write-Host "   Command: $composeCmd" -ForegroundColor DarkGray

Invoke-Expression $composeCmd

# Verify Claude is configured
if ($env:ANTHROPIC_API_KEY) {
    Write-Host "`n⏳ Waiting for coding-agent to start..." -ForegroundColor Cyan
    Start-Sleep -Seconds 5
    
    $logs = docker logs memory-coding-agent --tail 10 2>&1
    if ($logs -match "CLAUDE.*Configured|CLAUDE.*claude") {
        Write-Host "✅ Claude is configured and ready!" -ForegroundColor Green
    }
    else {
        Write-Host "⚠️ Check coding-agent logs for Claude status" -ForegroundColor Yellow
    }
}

Write-Host "`n✨ Done!" -ForegroundColor Green






