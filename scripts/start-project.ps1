# Memory Code Agent - Project-Specific Startup Script
# Usage: .\start-project.ps1 -ProjectPath "E:\GitHub\TradingSystem" [-AutoIndex] [-ProjectName "trading"]

param(
    [Parameter(Mandatory=$true, HelpMessage="Path to the project directory to index")]
    [string]$ProjectPath,
    
    [Parameter(Mandatory=$false, HelpMessage="Automatically index the project after startup")]
    [switch]$AutoIndex,
    
    [Parameter(Mandatory=$false, HelpMessage="Project name for container isolation (defaults to folder name, lowercase)")]
    [string]$ProjectName,
    
    [Parameter(Mandatory=$false, HelpMessage="Context name for memory (defaults to ProjectName)")]
    [string]$ContextName,
    
    [Parameter(Mandatory=$false, HelpMessage="MCP Server port (default: 5000 + offset based on project)")]
    [int]$McpPort = 0,
    
    [Parameter(Mandatory=$false, HelpMessage="Qdrant URL (default: uses containerized Qdrant)")]
    [string]$QdrantUrl = "",
    
    [Parameter(Mandatory=$false, HelpMessage="Neo4j URL (default: uses containerized Neo4j)")]
    [string]$Neo4jUrl = "",
    
    [Parameter(Mandatory=$false, HelpMessage="Neo4j Username (default: neo4j)")]
    [string]$Neo4jUser = "neo4j",
    
    [Parameter(Mandatory=$false, HelpMessage="Neo4j Password (default: memoryagent)")]
    [string]$Neo4jPassword = "memoryagent",
    
    [Parameter(Mandatory=$false, HelpMessage="Ollama URL (default: uses containerized Ollama)")]
    [string]$OllamaUrl = ""
)

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Memory Code Agent - Project Startup" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Validate project path
if (-not (Test-Path $ProjectPath)) {
    Write-Host "Error: Project path does not exist: $ProjectPath" -ForegroundColor Red
    exit 1
}

$ProjectPath = (Resolve-Path $ProjectPath).Path
Write-Host "Project Path: $ProjectPath" -ForegroundColor Yellow

# Determine project name (lowercase, alphanumeric only)
if (-not $ProjectName) {
    $ProjectName = (Split-Path -Leaf $ProjectPath).ToLower() -replace '[^a-z0-9]', ''
}
$ProjectName = $ProjectName.ToLower() -replace '[^a-z0-9]', ''

# Determine context name
if (-not $ContextName) {
    $ContextName = Split-Path -Leaf $ProjectPath
}

# Calculate unique port offset based on project name hash (to avoid conflicts)
if ($McpPort -eq 0) {
    $hash = 0
    foreach ($char in $ProjectName.ToCharArray()) {
        $hash += [int]$char
    }
    $portOffset = $hash % 100
    $McpPort = 5000 + $portOffset
}

$QdrantHttpPort = 6333 + ($McpPort - 5000)
$QdrantGrpcPort = 6334 + ($McpPort - 5000)
$Neo4jHttpPort = 7474 + ($McpPort - 5000)
$Neo4jBoltPort = 7687 + ($McpPort - 5000)
$OllamaPort = 11434 + ($McpPort - 5000)

Write-Host "Project Name: $ProjectName" -ForegroundColor Yellow
Write-Host "Context Name: $ContextName" -ForegroundColor Yellow
Write-Host "MCP Port: $McpPort" -ForegroundColor Yellow
Write-Host ""

# Convert Windows path to container path (needed early for env vars)
$folderName = Split-Path -Leaf $ProjectPath
$containerPath = "/workspace/$folderName"

# Set environment variables for docker-compose
$env:PROJECT_NAME = $ProjectName
$env:PROJECT_PATH = Split-Path -Parent $ProjectPath
$env:MCP_PORT = $McpPort
$env:QDRANT_HTTP_PORT = $QdrantHttpPort
$env:QDRANT_GRPC_PORT = $QdrantGrpcPort
$env:NEO4J_HTTP_PORT = $Neo4jHttpPort
$env:NEO4J_BOLT_PORT = $Neo4jBoltPort
$env:OLLAMA_PORT = $OllamaPort
$env:NEO4J_PASSWORD = $Neo4jPassword
$env:CONTEXT_NAME = $ContextName
$env:CONTAINER_PATH = $containerPath

# Set external service URLs if provided (otherwise defaults to containerized services)
if ($QdrantUrl) {
    $env:QDRANT_URL = $QdrantUrl
    Write-Host "Using external Qdrant: $QdrantUrl" -ForegroundColor Yellow
}
if ($Neo4jUrl) {
    $env:NEO4J_URL = $Neo4jUrl
    Write-Host "Using external Neo4j: $Neo4jUrl" -ForegroundColor Yellow
}
if ($Neo4jUser -ne "neo4j") {
    $env:NEO4J_USER = $Neo4jUser
}
if ($OllamaUrl) {
    $env:OLLAMA_URL = $OllamaUrl
    Write-Host "Using external Ollama: $OllamaUrl" -ForegroundColor Yellow
}

# Ensure storage directory exists - always create/verify
$storageRoot = "d:\Memory\$ProjectName"
Write-Host "Ensuring storage directories exist: $storageRoot" -ForegroundColor Cyan

# Create all required directories (Force will create if missing, no error if exists)
New-Item -ItemType Directory -Path "$storageRoot\memory" -Force | Out-Null
New-Item -ItemType Directory -Path "$storageRoot\logs" -Force | Out-Null
New-Item -ItemType Directory -Path "$storageRoot\qdrant" -Force | Out-Null
New-Item -ItemType Directory -Path "$storageRoot\neo4j\data" -Force | Out-Null
New-Item -ItemType Directory -Path "$storageRoot\neo4j\logs" -Force | Out-Null
New-Item -ItemType Directory -Path "$storageRoot\neo4j\import" -Force | Out-Null
New-Item -ItemType Directory -Path "$storageRoot\neo4j\plugins" -Force | Out-Null
New-Item -ItemType Directory -Path "$storageRoot\ollama" -Force | Out-Null

Write-Host "Storage directories verified/created" -ForegroundColor Green

# Check if containers are already running
Write-Host "Checking container status..." -ForegroundColor Cyan
$containerName = "$ProjectName-agent-server"
$running = docker ps --filter "name=$containerName" --filter "status=running" --format "{{.Names}}" 2>$null

if ($running -eq $containerName) {
    Write-Host "Containers already running for project: $ProjectName" -ForegroundColor Green
} else {
    # Start containers
    Write-Host "Starting Docker containers for project: $ProjectName..." -ForegroundColor Cyan
    docker-compose up -d
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Error: Failed to start containers" -ForegroundColor Red
        exit 1
    }
    
    # Wait for services to be ready
    Write-Host ""
    Write-Host "Waiting for services to initialize..." -ForegroundColor Cyan
    Start-Sleep -Seconds 10
}

# Check if Ollama model exists
Write-Host "Checking Ollama model..." -ForegroundColor Cyan
$ollamaContainer = "$ProjectName-agent-ollama"
$modelCheck = docker exec $ollamaContainer ollama list 2>&1 | Select-String "mxbai-embed-large"
if (-not $modelCheck) {
    Write-Host "Downloading mxbai-embed-large model (this may take a few minutes)..." -ForegroundColor Yellow
    docker exec $ollamaContainer ollama pull mxbai-embed-large:latest
} else {
    Write-Host "Model already downloaded" -ForegroundColor Green
}

# Show status
Write-Host ""
Write-Host "Service Status:" -ForegroundColor Green
docker-compose ps

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Memory Code Agent is ready!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Project Configuration:" -ForegroundColor Yellow
Write-Host "  Project Name:    $ProjectName" -ForegroundColor White
Write-Host "  Windows Path:    $ProjectPath" -ForegroundColor White
Write-Host "  Container Path:  $containerPath" -ForegroundColor White
Write-Host "  Context Name:    $ContextName" -ForegroundColor White
Write-Host ""
Write-Host "Access Points:" -ForegroundColor Yellow
Write-Host "  MCP Server:      http://localhost:$McpPort" -ForegroundColor White

if ($QdrantUrl) {
    Write-Host "  Qdrant:          $QdrantUrl (external)" -ForegroundColor Cyan
} else {
    Write-Host "  Qdrant:          http://localhost:$QdrantHttpPort/dashboard" -ForegroundColor White
}

if ($Neo4jUrl) {
    Write-Host "  Neo4j:           $Neo4jUrl (external, user: $Neo4jUser)" -ForegroundColor Cyan
} else {
    Write-Host "  Neo4j Browser:   http://localhost:$Neo4jHttpPort (user: $Neo4jUser, password: $Neo4jPassword)" -ForegroundColor White
}

if ($OllamaUrl) {
    Write-Host "  Ollama:          $OllamaUrl (external)" -ForegroundColor Cyan
} else {
    Write-Host "  Ollama:          http://localhost:$OllamaPort" -ForegroundColor White
}

Write-Host ""
Write-Host "Storage Location:" -ForegroundColor Yellow
Write-Host "  $storageRoot" -ForegroundColor White
Write-Host ""

# Auto-index if requested
if ($AutoIndex) {
    Write-Host ""
    Write-Host "Waiting for MCP server to be ready..." -ForegroundColor Cyan
    
    $maxRetries = 60
    $retryCount = 0
    $serverReady = $false
    
    while ($retryCount -lt $maxRetries -and -not $serverReady) {
        try {
            # Use -SkipHttpErrorCheck to allow 404 responses (PowerShell 7+)
            $response = Invoke-WebRequest -Uri "http://localhost:$McpPort" -Method GET -TimeoutSec 2 -SkipHttpErrorCheck -ErrorAction Stop
            if ($response.StatusCode -eq 200 -or $response.StatusCode -eq 404) {
                $serverReady = $true
                Write-Host "MCP server is ready!" -ForegroundColor Green
            }
        } catch {
            $retryCount++
            if ($retryCount % 10 -eq 0) {
                Write-Host "  Still waiting... ($retryCount/$maxRetries)" -ForegroundColor Gray
            }
            Start-Sleep -Seconds 2
        }
    }
    
    if (-not $serverReady) {
        Write-Host "MCP server did not become ready in time" -ForegroundColor Red
        Write-Host "  You can manually index later" -ForegroundColor Yellow
    } else {
        Write-Host "Starting background indexing job..." -ForegroundColor Cyan
        
        # Start indexing in background job
        $job = Start-Job -ScriptBlock {
            param($port, $path, $context)
            
            $body = @{
                path = $path
                recursive = $true
                context = $context
            } | ConvertTo-Json
            
            Invoke-RestMethod -Uri "http://localhost:$port/api/index/directory" `
                -Method POST `
                -Body $body `
                -ContentType "application/json" `
                -TimeoutSec 7200
        } -ArgumentList $McpPort, $containerPath, $ContextName -Name "Index-$ProjectName"
        
        Write-Host "  Job started: Index-$ProjectName (ID: $($job.Id))" -ForegroundColor Green
        Write-Host ""
        Write-Host "Check indexing progress:" -ForegroundColor Yellow
        Write-Host "  Get-Job -Name 'Index-$ProjectName' | Receive-Job -Keep" -ForegroundColor Gray
        Write-Host ""
        Write-Host "Wait for completion:" -ForegroundColor Yellow
        Write-Host "  Wait-Job -Name 'Index-$ProjectName'; Receive-Job -Name 'Index-$ProjectName'" -ForegroundColor Gray
    }
    Write-Host ""
}

Write-Host "Quick Commands:" -ForegroundColor Yellow
Write-Host ""
Write-Host "Index Project:" -ForegroundColor Cyan
Write-Host "  `$body = @{path='$containerPath';recursive=`$true;context='$ContextName'} | ConvertTo-Json" -ForegroundColor Gray
Write-Host "  Invoke-RestMethod -Uri http://localhost:$McpPort/api/index/directory -Method POST -Body `$body -ContentType 'application/json'" -ForegroundColor Gray
Write-Host ""
Write-Host "Reindex Project:" -ForegroundColor Cyan
Write-Host "  `$body = @{path='$containerPath';context='$ContextName'} | ConvertTo-Json" -ForegroundColor Gray
Write-Host "  Invoke-RestMethod -Uri http://localhost:$McpPort/api/reindex -Method POST -Body `$body -ContentType 'application/json'" -ForegroundColor Gray
Write-Host ""
Write-Host "Query Code:" -ForegroundColor Cyan
Write-Host "  `$body = @{query='your question';context='$ContextName'} | ConvertTo-Json" -ForegroundColor Gray
Write-Host "  Invoke-RestMethod -Uri http://localhost:$McpPort/api/query -Method POST -Body `$body -ContentType 'application/json'" -ForegroundColor Gray
Write-Host ""
Write-Host "Stop Services:" -ForegroundColor Cyan
Write-Host "  docker-compose down" -ForegroundColor Gray
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan

# Save project config for later use
$configPath = ".\projects\$ProjectName.json"
$configDir = Split-Path -Parent $configPath
if (-not (Test-Path $configDir)) {
    New-Item -ItemType Directory -Path $configDir -Force | Out-Null
}

$projectConfig = @{
    ProjectName = $ProjectName
    ProjectPath = $ProjectPath
    ContainerPath = $containerPath
    ContextName = $ContextName
    McpPort = $McpPort
    QdrantHttpPort = $QdrantHttpPort
    QdrantGrpcPort = $QdrantGrpcPort
    Neo4jHttpPort = $Neo4jHttpPort
    Neo4jBoltPort = $Neo4jBoltPort
    OllamaPort = $OllamaPort
    IndexedAt = (Get-Date).ToString("o")
} | ConvertTo-Json

$projectConfig | Out-File -FilePath $configPath -Encoding UTF8
Write-Host "Project configuration saved to: $configPath" -ForegroundColor Green

# Also save as "last project" for quick restart
$projectConfig | Out-File -FilePath ".\last-project.json" -Encoding UTF8
