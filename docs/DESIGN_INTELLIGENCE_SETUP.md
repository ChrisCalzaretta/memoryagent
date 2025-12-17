# 🚀 Design Intelligence System - Setup Guide

> Complete setup, deployment, and operational guide for the Design Intelligence System.

---

## Table of Contents

1. [System Requirements](#system-requirements)
2. [Dependencies](#dependencies)
3. [Installation](#installation)
4. [Docker Services](#docker-services)
5. [Configuration](#configuration)
6. [Initial Data Seeding](#initial-data-seeding)
7. [Running the System](#running-the-system)
8. [Monitoring & Troubleshooting](#monitoring--troubleshooting)
9. [Storage Requirements](#storage-requirements)
10. [Performance Tuning](#performance-tuning)

---

## System Requirements

### Minimum Specifications

| Component | Minimum | Recommended |
|-----------|---------|-------------|
| **CPU** | 4 cores (8 threads) | 8 cores (16 threads) |
| **RAM** | 16 GB | 32 GB |
| **GPU** | NVIDIA 6GB VRAM | NVIDIA 8GB+ VRAM |
| **Disk** | 100 GB SSD | 500 GB NVMe SSD |
| **Network** | 10 Mbps | 100 Mbps |
| **OS** | Linux, macOS, Windows (WSL2) | Linux (Ubuntu 22.04+) |

### GPU Requirements

**For LLaVA (Vision Model):**
- NVIDIA GPU with **6GB+ VRAM** (minimum)
- CUDA 11.8+ or ROCm 5.4+
- Drivers: NVIDIA 525+ or AMD equivalent

**Supported GPUs:**
- ✅ NVIDIA RTX 3060 (12GB) - Excellent
- ✅ NVIDIA RTX 3070 (8GB) - Good
- ✅ NVIDIA RTX 4060 Ti (8GB) - Good
- ✅ NVIDIA RTX 4070 (12GB) - Excellent
- ⚠️ NVIDIA GTX 1660 Ti (6GB) - Minimum (slower)
- ❌ No GPU - Not supported (too slow)

### Network Requirements

**Outbound Access Required:**
- Google Search API (search queries)
- Web crawling targets (screenshots, DOM)
- Ollama model downloads (first time)
- Neo4j Aura (if using cloud)
- Qdrant Cloud (if using cloud)

**Ports:**
- `5000` - DesignAgent.Server (ASP.NET)
- `11434` - Ollama API
- `7687` - Neo4j Bolt
- `6333` - Qdrant HTTP
- `6334` - Qdrant gRPC

---

## Dependencies

### Core Dependencies

#### 1. .NET 8.0 SDK
**Version:** 8.0.100+

**Installation:**
```bash
# Windows (winget)
winget install Microsoft.DotNet.SDK.8

# Linux (Ubuntu/Debian)
wget https://dot.net/v1/dotnet-install.sh
chmod +x dotnet-install.sh
./dotnet-install.sh --channel 8.0

# macOS (Homebrew)
brew install dotnet@8
```

**Verify:**
```bash
dotnet --version
# Output: 8.0.xxx
```

---

#### 2. Ollama (LLM Platform)
**Version:** 0.1.20+

**Installation:**
```bash
# Linux
curl -fsSL https://ollama.com/install.sh | sh

# macOS
brew install ollama

# Windows
# Download from: https://ollama.com/download/windows
```

**Verify:**
```bash
ollama --version
# Output: ollama version 0.1.20+
```

**Required Models:**
```bash
# Vision model (13B, ~8GB download)
ollama pull llava:13b

# Text models
ollama pull deepseek-v2:16b  # Code generation
ollama pull phi4              # Validation, analysis
ollama pull qwen2.5:7b        # Backup model
```

**Model Disk Space:**
| Model | Size | Purpose |
|-------|------|---------|
| `llava:13b` | 8.0 GB | Visual analysis (screenshots) |
| `deepseek-v2:16b` | 9.5 GB | Code generation, synthesis |
| `phi4` | 4.5 GB | Validation, feedback analysis |
| `qwen2.5:7b` | 4.0 GB | Backup/fallback |
| **Total** | **26 GB** | |

**Ollama Configuration:**
```bash
# Set GPU allocation (optional)
export OLLAMA_GPU_LAYERS=40
export OLLAMA_NUM_PARALLEL=2
export OLLAMA_MAX_LOADED_MODELS=2

# Start Ollama service
ollama serve
```

---

#### 3. Playwright (Web Crawling)
**Version:** 1.40.0+

Playwright is installed via NuGet, but requires browser binaries:

```bash
# After building DesignAgent.Server
cd DesignAgent.Server
dotnet build

# Install Chromium browser
pwsh bin/Debug/net8.0/playwright.ps1 install chromium

# Or on Linux/macOS
./bin/Debug/net8.0/playwright.sh install chromium
```

**Browser Disk Space:**
- Chromium: ~300 MB

**Playwright Dependencies (Linux only):**
```bash
# Ubuntu/Debian
sudo apt-get install -y \
    libnss3 libnspr4 libatk1.0-0 libatk-bridge2.0-0 \
    libcups2 libdrm2 libxkbcommon0 libxcomposite1 \
    libxdamage1 libxfixes3 libxrandr2 libgbm1 libasound2
```

---

#### 4. Neo4j (Knowledge Graph)
**Version:** 5.15.0+

**Option A: Docker (Recommended)**
```yaml
# docker-compose.yml
neo4j:
  image: neo4j:5.15.0
  ports:
    - "7474:7474"  # Web UI
    - "7687:7687"  # Bolt
  environment:
    - NEO4J_AUTH=neo4j/password123
    - NEO4J_PLUGINS=["apoc", "graph-data-science"]
    - NEO4J_dbms_memory_heap_initial__size=1G
    - NEO4J_dbms_memory_heap_max__size=4G
  volumes:
    - neo4j_data:/data
```

**Option B: Standalone**
```bash
# Linux
wget https://neo4j.com/artifact.php?name=neo4j-community-5.15.0-unix.tar.gz
tar -xf neo4j-community-5.15.0-unix.tar.gz
cd neo4j-community-5.15.0
bin/neo4j start

# macOS
brew install neo4j
neo4j start
```

**Initial Setup:**
```bash
# Access web UI: http://localhost:7474
# Default: neo4j / neo4j
# Change password to: password123 (or your choice)
```

---

#### 5. Qdrant (Vector Database)
**Version:** 1.7.0+

**Option A: Docker (Recommended)**
```yaml
# docker-compose.yml
qdrant:
  image: qdrant/qdrant:v1.7.4
  ports:
    - "6333:6333"  # HTTP
    - "6334:6334"  # gRPC
  volumes:
    - qdrant_data:/qdrant/storage
```

**Option B: Standalone**
```bash
# Linux/macOS
docker run -p 6333:6333 -p 6334:6334 \
    -v $(pwd)/qdrant_storage:/qdrant/storage \
    qdrant/qdrant:v1.7.4
```

**Verify:**
```bash
curl http://localhost:6333/collections
# Output: {"result":{"collections":[]}, "status":"ok", "time":0.001}
```

---

### NuGet Packages (Automatically Installed)

These are referenced in `DesignAgent.Server.csproj` and installed via `dotnet restore`:

| Package | Version | Purpose |
|---------|---------|---------|
| `Microsoft.Playwright` | 1.40.0 | Web crawling, screenshots |
| `Neo4j.Driver` | 5.15.0 | Neo4j client |
| `Qdrant.Client` | 1.7.0 | Qdrant vector DB client |
| `Swashbuckle.AspNetCore` | 6.5.0 | OpenAPI/Swagger |
| `Microsoft.Extensions.Http` | 8.0.0 | HTTP client factory |
| `Serilog.AspNetCore` | 8.0.0 | Logging |

---

## Installation

### 1. Clone Repository

```bash
git clone https://github.com/your-org/MemoryAgent.git
cd MemoryAgent
```

---

### 2. Install Dependencies

```bash
# Install .NET dependencies
dotnet restore

# Install Playwright browsers
cd DesignAgent.Server
pwsh bin/Debug/net8.0/playwright.ps1 install chromium
cd ..
```

---

### 3. Start Infrastructure (Docker)

```bash
# Start Neo4j + Qdrant + Ollama
docker-compose up -d neo4j qdrant ollama

# Verify services
docker-compose ps
```

**Expected Output:**
```
NAME                SERVICE   STATUS    PORTS
memoryagent-neo4j   neo4j     Up        0.0.0.0:7474->7474, 0.0.0.0:7687->7687
memoryagent-qdrant  qdrant    Up        0.0.0.0:6333->6333, 0.0.0.0:6334->6334
memoryagent-ollama  ollama    Up        0.0.0.0:11434->11434
```

---

### 4. Pull Ollama Models

```bash
# Pull all required models (wait ~15 min for downloads)
docker exec -it memoryagent-ollama ollama pull llava:13b
docker exec -it memoryagent-ollama ollama pull deepseek-v2:16b
docker exec -it memoryagent-ollama ollama pull phi4
docker exec -it memoryagent-ollama ollama pull qwen2.5:7b
```

---

### 5. Configure Application

Edit `DesignAgent.Server/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "Neo4j": "bolt://localhost:7687",
    "Qdrant": "http://localhost:6333"
  },
  "Neo4jCredentials": {
    "Username": "neo4j",
    "Password": "password123"
  },
  "Ollama": {
    "BaseUrl": "http://localhost:11434",
    "DefaultModel": "llava:13b",
    "Timeout": 120
  },
  "DesignIntelligence": {
    "InitialThreshold": 7.0,
    "LeaderboardSize": 100,
    "MaxPagesPerSite": 6,
    "CrawlDelayMs": 2000,
    "MaxCpuPercent": 30,
    "SearchQueriesPerRun": 5,
    "ScreenshotPath": "./data/screenshots"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "DesignAgent": "Debug"
    }
  }
}
```

---

### 6. Build and Run

```bash
# Build solution
dotnet build

# Run DesignAgent.Server
cd DesignAgent.Server
dotnet run

# Or use watch mode (auto-reload on changes)
dotnet watch run
```

**Expected Output:**
```
info: DesignAgent.Server.Program[0]
      🎨 DesignAgent.Server starting...
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5000
info: DesignAgent.Server.Services.DesignIntelligenceService[0]
      🚀 Design Intelligence System initialized
      - Leaderboard: 0/100 designs
      - Threshold: 7.0
      - Background learning: ENABLED
```

---

## Docker Services

### Full Docker Compose Configuration

**File:** `docker-compose.yml`

```yaml
version: '3.8'

services:
  # Neo4j - Knowledge Graph
  neo4j:
    image: neo4j:5.15.0
    container_name: memoryagent-neo4j
    ports:
      - "7474:7474"  # Web UI
      - "7687:7687"  # Bolt
    environment:
      - NEO4J_AUTH=neo4j/password123
      - NEO4J_PLUGINS=["apoc", "graph-data-science"]
      - NEO4J_dbms_memory_heap_initial__size=1G
      - NEO4J_dbms_memory_heap_max__size=4G
      - NEO4J_dbms_security_procedures_unrestricted=apoc.*,gds.*
    volumes:
      - neo4j_data:/data
      - neo4j_logs:/logs
    restart: unless-stopped

  # Qdrant - Vector Database
  qdrant:
    image: qdrant/qdrant:v1.7.4
    container_name: memoryagent-qdrant
    ports:
      - "6333:6333"  # HTTP
      - "6334:6334"  # gRPC
    volumes:
      - qdrant_data:/qdrant/storage
    restart: unless-stopped

  # Ollama - LLM Platform
  ollama:
    image: ollama/ollama:latest
    container_name: memoryagent-ollama
    ports:
      - "11434:11434"
    volumes:
      - ollama_models:/root/.ollama
    deploy:
      resources:
        reservations:
          devices:
            - driver: nvidia
              count: 1
              capabilities: [gpu]
    restart: unless-stopped

  # DesignAgent.Server (optional - can run locally)
  design-agent:
    build:
      context: .
      dockerfile: DesignAgent.Server/Dockerfile
    container_name: memoryagent-design-agent
    ports:
      - "5000:8080"
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ConnectionStrings__Neo4j=bolt://neo4j:7687
      - ConnectionStrings__Qdrant=http://qdrant:6333
      - Neo4jCredentials__Username=neo4j
      - Neo4jCredentials__Password=password123
      - Ollama__BaseUrl=http://ollama:11434
    depends_on:
      - neo4j
      - qdrant
      - ollama
    restart: unless-stopped

volumes:
  neo4j_data:
  neo4j_logs:
  qdrant_data:
  ollama_models:
```

### Start All Services

```bash
# Start infrastructure only
docker-compose up -d neo4j qdrant ollama

# Start everything (including DesignAgent)
docker-compose up -d

# View logs
docker-compose logs -f design-agent

# Stop all services
docker-compose down

# Stop and remove volumes (CAUTION: deletes data)
docker-compose down -v
```

---

## Configuration

### Environment Variables

You can override `appsettings.json` via environment variables:

```bash
# Connection strings
export ConnectionStrings__Neo4j="bolt://localhost:7687"
export ConnectionStrings__Qdrant="http://localhost:6333"

# Neo4j credentials
export Neo4jCredentials__Username="neo4j"
export Neo4jCredentials__Password="your-secure-password"

# Ollama
export Ollama__BaseUrl="http://localhost:11434"
export Ollama__DefaultModel="llava:13b"

# Design Intelligence
export DesignIntelligence__InitialThreshold="7.0"
export DesignIntelligence__LeaderboardSize="100"
export DesignIntelligence__MaxCpuPercent="30"

# Run
dotnet run
```

---

### Configuration Options

**Full `DesignIntelligence` section:**

```json
{
  "DesignIntelligence": {
    // Quality gate threshold (0-10 scale)
    "InitialThreshold": 7.0,
    
    // Max designs to keep in leaderboard
    "LeaderboardSize": 100,
    
    // Max pages to crawl per site (1-10)
    "MaxPagesPerSite": 6,
    
    // Delay between page crawls (milliseconds)
    "CrawlDelayMs": 2000,
    
    // CPU usage limit for background service (%)
    "MaxCpuPercent": 30,
    
    // Search queries per discovery run
    "SearchQueriesPerRun": 5,
    
    // Screenshot storage path
    "ScreenshotPath": "./data/screenshots",
    
    // Screenshot quality (0-100)
    "ScreenshotQuality": 85,
    
    // Screenshot breakpoints
    "ScreenshotBreakpoints": [1920, 1024, 375],
    
    // Enable video capture for animations
    "EnableVideoCapture": false,
    
    // Max video duration (seconds)
    "VideoCaptureDurationSec": 10,
    
    // Playwright timeout (milliseconds)
    "PlaywrightTimeoutMs": 30000,
    
    // Search API provider ("google", "bing", "serper")
    "SearchProvider": "google",
    
    // Search API key (required)
    "SearchApiKey": "your-api-key-here",
    
    // Enable background learning service
    "EnableBackgroundLearning": true,
    
    // Background service interval (seconds)
    "BackgroundIntervalSec": 3600
  }
}
```

---

## Initial Data Seeding

### 1. Seed Prompts

The system needs **23 prompts** seeded into Neo4j's Lightning knowledge graph.

**Create:** `DesignAgent.Server/Data/design_prompts.json`

```json
[
  {
    "name": "design_source_discovery",
    "category": "discovery",
    "systemPrompt": "You are a design discovery expert...",
    "userPromptTemplate": "Generate 5 search queries to find {category} designs...",
    "version": 1
  },
  {
    "name": "design_source_evaluation",
    "category": "discovery",
    "systemPrompt": "You are a design source evaluator...",
    "userPromptTemplate": "Evaluate this search result...",
    "version": 1
  }
  // ... (21 more prompts)
]
```

**Seed via API:**

```bash
# POST to Memory Agent Lightning endpoint
curl -X POST http://localhost:5001/api/lightning/prompts/seed \
  -H "Content-Type: application/json" \
  -d @DesignAgent.Server/Data/design_prompts.json
```

---

### 2. Seed Curated Sources

Seed 100 high-quality design sources to bootstrap the system.

**Create:** `DesignAgent.Server/Data/curated_sources.json`

```json
[
  {
    "url": "https://linear.app",
    "category": "saas",
    "trustScore": 10,
    "tags": ["gradient", "minimal", "animations"]
  },
  {
    "url": "https://vercel.com",
    "category": "developer-tools",
    "trustScore": 10,
    "tags": ["dark-mode", "minimal", "technical"]
  },
  {
    "url": "https://stripe.com",
    "category": "fintech",
    "trustScore": 10,
    "tags": ["trust", "documentation", "clean"]
  }
  // ... (97 more sources)
]
```

**Full Curated List (100 Sources):**

**SaaS & B2B (30)**
- linear.app, notion.so, figma.com, airtable.com, asana.com
- monday.com, clickup.com, miro.com, loom.com, calendly.com
- intercom.com, drift.com, hubspot.com, salesforce.com, slack.com
- zoom.us, dropbox.com, box.com, atlassian.com, trello.com
- basecamp.com, freshworks.com, zendesk.com, pipedrive.com, aircall.io
- front.com, customer.io, segment.com, mixpanel.com, amplitude.com

**Developer Tools (20)**
- vercel.com, netlify.com, github.com, gitlab.com, stripe.com
- twilio.com, sendgrid.com, heroku.com, railway.app, render.com
- planetscale.com, supabase.com, hasura.io, prisma.io, sanity.io
- contentful.com, algolia.com, auth0.com, clerk.dev, propelauth.com

**Design/Creative (20)**
- dribbble.com, behance.net, awwwards.com, siteinspire.com, godly.website
- lapa.ninja, landingfolio.com, saaslandingpage.com, uijar.com, mobbin.com
- scrnshts.club, screenlane.com, pageflows.com, saasframe.io, uigarage.net
- designspiration.com, niice.co, muzli.io, sidebar.io, webdesign-inspiration.com

**E-commerce (10)**
- shopify.com, allbirds.com, warbyparker.com, casper.com, glossier.com
- away.com, everlane.com, parachutehome.com, burrow.com, outer.com

**Finance (10)**
- stripe.com, plaid.com, wise.com, revolut.com, mercury.com
- brex.com, ramp.com, bench.co, gusto.com, carta.com

**Health/Fitness (10)**
- calm.com, headspace.com, strava.com, peloton.com, whoop.com
- noom.com, myfitnesspal.com, fitbit.com, withings.com, oura.com

**Design Systems (10)**
- material.io, primer.style, spectrum.adobe.com, polaris.shopify.com
- designsystem.digital.gov, carbondesignsystem.com, atlassian.design
- ux.shopify.com, design.gitlab.com, vercel.com/design

**Seed via MCP:**

```bash
# Use design_seed_sources MCP tool
{
  "sources": [...] # Array from curated_sources.json
}
```

---

## Running the System

### Development Mode

```bash
cd DesignAgent.Server
dotnet watch run

# Server runs at: http://localhost:5000
# Swagger UI: http://localhost:5000/swagger
```

---

### Production Mode

```bash
# Build optimized release
dotnet publish -c Release -o ./publish

# Run
cd publish
./DesignAgent.Server

# Or via systemd (Linux)
sudo systemctl start design-agent
```

---

### Background Learning Service

The Design Intelligence background service runs automatically when `EnableBackgroundLearning: true`.

**Check Status:**

```bash
# Via MCP tool: design_status
{
  "leaderboard": {
    "count": 42,
    "floor": 7.3,
    "ceiling": 9.5
  },
  "backgroundService": {
    "running": true,
    "lastRun": "2024-12-16T10:30:00Z",
    "sitesProcessed": 42,
    "sitesDiscarded": 18
  }
}
```

**Manual Trigger:**

```bash
# Via MCP tool: design_discover
{
  "count": 10  # Process 10 new sources now
}
```

---

## Monitoring & Troubleshooting

### Health Checks

**Endpoint:** `GET /health`

```bash
curl http://localhost:5000/health
```

**Response:**
```json
{
  "status": "Healthy",
  "checks": {
    "neo4j": "Healthy",
    "qdrant": "Healthy",
    "ollama": "Healthy",
    "playwright": "Healthy"
  }
}
```

---

### Logs

**View Logs:**

```bash
# Docker
docker-compose logs -f design-agent

# Local
tail -f logs/design-agent-*.log
```

**Key Log Messages:**

```
✅ Design passed quality gate (8.5 >= 7.0)
❌ Design discarded (6.2 < 7.0)
🧠 LLM analysis completed (3.2s, llava:13b)
🎯 Leaderboard updated: #42 evicted (7.1), #1 new entry (8.5)
⚠️  CPU usage 35% > 30% threshold, throttling...
🔄 Prompt evolved: design_analysis_homepage v2
```

---

### Common Issues

#### 1. Ollama Model Not Found

**Error:**
```
Error: model "llava:13b" not found
```

**Fix:**
```bash
docker exec -it memoryagent-ollama ollama pull llava:13b
```

---

#### 2. Playwright Browser Not Installed

**Error:**
```
Playwright.PlaywrightException: Executable doesn't exist at /path/to/chromium
```

**Fix:**
```bash
cd DesignAgent.Server
pwsh bin/Debug/net8.0/playwright.ps1 install chromium
```

---

#### 3. Neo4j Connection Failed

**Error:**
```
Neo4j.Driver.ServiceUnavailableException: Connection refused
```

**Fix:**
```bash
# Check Neo4j is running
docker-compose ps neo4j

# Restart if needed
docker-compose restart neo4j

# Check credentials in appsettings.json
```

---

#### 4. GPU Not Detected (Ollama)

**Error:**
```
WARN: GPU not found, using CPU (slow)
```

**Fix:**
```bash
# Install nvidia-container-toolkit
sudo apt-get install -y nvidia-container-toolkit
sudo systemctl restart docker

# Verify GPU accessible in container
docker exec -it memoryagent-ollama nvidia-smi
```

---

#### 5. High CPU Usage

**Symptom:** System sluggish, CPU > 50%

**Fix:**
```json
// appsettings.json
{
  "DesignIntelligence": {
    "MaxCpuPercent": 20,        // Lower threshold
    "CrawlDelayMs": 5000,       // Increase delay
    "BackgroundIntervalSec": 7200  // Run less frequently
  }
}
```

---

## Storage Requirements

### Disk Space Planning

**Per Design (6 pages × 3 breakpoints = 18 screenshots):**

| Component | Size | Calculation |
|-----------|------|-------------|
| Screenshots (18) | 9 MB | 500 KB × 18 |
| DOM/HTML | 1 MB | 50 KB × 18 pages |
| Video (optional) | 5 MB | 500 KB × 10 sec |
| Metadata | 100 KB | JSON, scores, etc. |
| **Total per design** | **15 MB** | |

**100 Designs:**
- Screenshots: 900 MB
- DOM/HTML: 100 MB
- Videos (optional): 500 MB
- Metadata: 10 MB
- **Total: ~1.5 GB**

**Database Storage:**

| Database | Storage | Notes |
|----------|---------|-------|
| Neo4j | 500 MB | Relationships, prompts, patterns |
| Qdrant | 2 GB | Vector embeddings |
| Ollama Models | 26 GB | llava, deepseek, phi4, qwen |
| **Total** | **28.5 GB** | |

**Recommended Disk Allocation:**
- Application: 100 GB SSD (room for growth)
- Databases: 50 GB SSD (separate partition if possible)

---

## Performance Tuning

### Ollama Optimization

**For GPU:**
```bash
export OLLAMA_GPU_LAYERS=40        # Use GPU for layers
export OLLAMA_NUM_PARALLEL=2       # Parallel requests
export OLLAMA_MAX_LOADED_MODELS=2  # Keep 2 models in VRAM
```

**For CPU (fallback):**
```bash
export OLLAMA_NUM_PARALLEL=1       # Sequential only
export OLLAMA_NUM_THREAD=8         # Match CPU cores
```

---

### Playwright Optimization

**Reduce Resource Usage:**

```json
{
  "PlaywrightOptions": {
    "Headless": true,
    "Args": [
      "--disable-dev-shm-usage",
      "--disable-gpu",
      "--no-sandbox",
      "--disable-setuid-sandbox"
    ]
  }
}
```

---

### Neo4j Optimization

**Memory Configuration:**

```yaml
# docker-compose.yml
environment:
  - NEO4J_dbms_memory_heap_initial__size=2G
  - NEO4J_dbms_memory_heap_max__size=8G
  - NEO4J_dbms_memory_pagecache__size=4G
```

**Indexes (run once):**

```cypher
// Neo4j Browser (http://localhost:7474)
CREATE INDEX design_url IF NOT EXISTS FOR (d:Design) ON (d.url);
CREATE INDEX prompt_name IF NOT EXISTS FOR (p:Prompt) ON (p.name);
CREATE INDEX pattern_score IF NOT EXISTS FOR (p:Pattern) ON (p.score);
```

---

### Qdrant Optimization

**Memory Configuration:**

```yaml
# docker-compose.yml
environment:
  - QDRANT__STORAGE__PERFORMANCE__OPTIMIZE_ON_RESTART=true
  - QDRANT__STORAGE__PERFORMANCE__MAX_CONCURRENT_REQUESTS=10
```

---

## Next Steps

### After Setup Complete

1. ✅ **Verify Health**: `curl http://localhost:5000/health`
2. ✅ **Seed Prompts**: Load 23 prompts into Lightning
3. ✅ **Seed Sources**: Load 100 curated sources
4. ✅ **Test MCP Tools**: Call `design_discover` to start learning
5. ✅ **Monitor Logs**: Watch first few designs being analyzed
6. ✅ **Check Leaderboard**: Call `design_leaderboard` after 1 hour

### Ongoing Operations

- **Weekly**: Review leaderboard floor (should rise over time)
- **Monthly**: Check prompt evolution (versions should increment)
- **Quarterly**: Prune low-performing sources (trust score < 5)
- **Backups**: Backup Neo4j + Qdrant volumes weekly

---

## Support & Resources

### Documentation
- Main Plan: `docs/DESIGN_INTELLIGENCE_PLAN.md`
- Scoring Details: `docs/DESIGN_INTELLIGENCE_SCORING.md`
- API Reference: `http://localhost:5000/swagger`

### Logs
- Application: `logs/design-agent-*.log`
- Neo4j: `docker-compose logs neo4j`
- Ollama: `docker-compose logs ollama`

### Troubleshooting
- Health endpoint: `http://localhost:5000/health`
- Neo4j Browser: `http://localhost:7474`
- Qdrant Dashboard: `http://localhost:6333/dashboard`

---

*Document Version: 1.0*
*Last Updated: December 2024*

