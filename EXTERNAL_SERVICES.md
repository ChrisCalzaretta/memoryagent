# 🌐 Using External Qdrant and Neo4j Services

## Overview

By default, Memory Code Agent runs Qdrant, Neo4j, and Ollama in Docker containers. However, you can now **point to external instances** of these services using command-line parameters!

---

## 🎯 Use Cases

### When to Use External Services:

1. **Shared Infrastructure**: Multiple projects sharing one Qdrant/Neo4j instance
2. **Cloud Hosting**: Using managed Neo4j Aura, cloud-hosted Qdrant, etc.
3. **Development**: Connecting to a dev server instead of local containers
4. **Production**: Pointing to production-grade database instances
5. **Resource Optimization**: Don't run heavy services locally

---

## 🚀 Usage

### Basic Syntax

```powershell
.\start-project.ps1 `
    -ProjectPath "E:\GitHub\CBC_AI" `
    -ProjectName "cbcai" `
    -QdrantUrl "http://qdrant-server:6334" `
    -Neo4jUrl "bolt://neo4j-server:7687" `
    -Neo4jUser "admin" `
    -Neo4jPassword "secure-password"
```

### Parameters

| Parameter | Description | Default |
|-----------|-------------|---------|
| `-QdrantUrl` | Qdrant gRPC URL | `http://localhost:6334` (containerized) |
| `-Neo4jUrl` | Neo4j Bolt URL | `bolt://localhost:7687` (containerized) |
| `-Neo4jUser` | Neo4j username | `neo4j` |
| `-Neo4jPassword` | Neo4j password | `memoryagent` |
| `-OllamaUrl` | Ollama API URL | `http://localhost:11434` (containerized) |

---

## 📝 Examples

### Example 1: External Qdrant Only

```powershell
.\start-project.ps1 `
    -ProjectPath "E:\GitHub\CBC_AI" `
    -ProjectName "cbcai" `
    -QdrantUrl "http://192.168.1.100:6334"
```

**Result:**
- ✅ MCP Server: Containerized
- ✅ Qdrant: **External** (192.168.1.100:6334)
- ✅ Neo4j: Containerized
- ✅ Ollama: Containerized

---

### Example 2: External Neo4j Only

```powershell
.\start-project.ps1 `
    -ProjectPath "E:\GitHub\CBC_AI" `
    -ProjectName "cbcai" `
    -Neo4jUrl "bolt://neo4j.example.com:7687" `
    -Neo4jUser "myuser" `
    -Neo4jPassword "mypassword"
```

**Result:**
- ✅ MCP Server: Containerized
- ✅ Qdrant: Containerized
- ✅ Neo4j: **External** (neo4j.example.com:7687)
- ✅ Ollama: Containerized

---

### Example 3: External Qdrant + Neo4j

```powershell
.\start-project.ps1 `
    -ProjectPath "E:\GitHub\CBC_AI" `
    -ProjectName "cbcai" `
    -QdrantUrl "http://qdrant-prod.example.com:6334" `
    -Neo4jUrl "neo4j+s://abc123.databases.neo4j.io:7687" `
    -Neo4jUser "neo4j" `
    -Neo4jPassword "super-secret-password"
```

**Result:**
- ✅ MCP Server: Containerized
- ✅ Qdrant: **External** (qdrant-prod.example.com)
- ✅ Neo4j: **External** (Neo4j Aura)
- ✅ Ollama: Containerized

---

### Example 4: All External Services

```powershell
.\start-project.ps1 `
    -ProjectPath "E:\GitHub\CBC_AI" `
    -ProjectName "cbcai" `
    -QdrantUrl "http://qdrant.internal:6334" `
    -Neo4jUrl "bolt://neo4j.internal:7687" `
    -Neo4jUser "admin" `
    -Neo4jPassword "admin123" `
    -OllamaUrl "http://ollama.internal:11434"
```

**Result:**
- ✅ MCP Server: Containerized (only thing running locally!)
- ✅ Qdrant: **External**
- ✅ Neo4j: **External**
- ✅ Ollama: **External**

---

## 🌍 Cloud Service Examples

### Neo4j Aura (Cloud)

```powershell
.\start-project.ps1 `
    -ProjectPath "E:\GitHub\CBC_AI" `
    -ProjectName "cbcai" `
    -Neo4jUrl "neo4j+s://xxxx.databases.neo4j.io:7687" `
    -Neo4jUser "neo4j" `
    -Neo4jPassword "your-aura-password"
```

**Note:** Neo4j Aura uses TLS (`neo4j+s://` or `bolt+s://`)

---

### Qdrant Cloud

```powershell
.\start-project.ps1 `
    -ProjectPath "E:\GitHub\CBC_AI" `
    -ProjectName "cbcai" `
    -QdrantUrl "https://xxxx-xxxx.aws.cloud.qdrant.io:6334"
```

---

### Local Network Servers

```powershell
# Qdrant on another machine
.\start-project.ps1 `
    -ProjectPath "E:\GitHub\CBC_AI" `
    -ProjectName "cbcai" `
    -QdrantUrl "http://192.168.1.50:6334" `
    -Neo4jUrl "bolt://192.168.1.51:7687" `
    -Neo4jUser "neo4j" `
    -Neo4jPassword "password"
```

---

## 🔧 Configuration Details

### Qdrant URL Format

```
Qdrant uses gRPC, so use the gRPC port (6334 by default)

http://hostname:6334     (standard)
https://hostname:6334    (TLS)
```

### Neo4j URL Format

```
Neo4j uses Bolt protocol

bolt://hostname:7687         (standard)
bolt+s://hostname:7687       (TLS)
neo4j://hostname:7687        (routing)
neo4j+s://hostname:7687      (routing + TLS)
```

### Ollama URL Format

```
Ollama uses HTTP API

http://hostname:11434        (standard)
https://hostname:11434       (TLS)
```

---

## 📊 What Happens Behind the Scenes

### With Default (Containerized) Services

```yaml
# docker-compose.yml uses internal container names
Qdrant__Url: "http://cbcai-agent-qdrant:6334"
Neo4j__Url: "bolt://cbcai-agent-neo4j:7687"
Ollama__Url: "http://cbcai-agent-ollama:11434"
```

### With External Services

```yaml
# Environment variables override defaults
Qdrant__Url: "http://external-qdrant:6334"
Neo4j__Url: "bolt://external-neo4j:7687"
Ollama__Url: "http://external-ollama:11434"
```

The MCP Server container uses these environment variables in `appsettings.json`.

---

## 🎯 Benefits of External Services

### 1. **Shared Database Across Projects**

```powershell
# Project 1
.\start-project.ps1 -ProjectPath "E:\GitHub\Project1" -Neo4jUrl "bolt://shared-neo4j:7687"

# Project 2 (shares same Neo4j)
.\start-project.ps1 -ProjectPath "E:\GitHub\Project2" -Neo4jUrl "bolt://shared-neo4j:7687"
```

Both projects use the same Neo4j instance but different contexts!

---

### 2. **Production-Grade Services**

```powershell
# Use managed Neo4j Aura for reliability
.\start-project.ps1 `
    -ProjectPath "E:\GitHub\CBC_AI" `
    -Neo4jUrl "neo4j+s://prod.databases.neo4j.io:7687" `
    -Neo4jUser "prod-user" `
    -Neo4jPassword "prod-password"
```

---

### 3. **Resource Optimization**

Run lightweight services locally, heavy ones on powerful servers:

```powershell
# Ollama on GPU server, others local
.\start-project.ps1 `
    -ProjectPath "E:\GitHub\CBC_AI" `
    -OllamaUrl "http://gpu-server:11434"
```

---

## 🔒 Security Considerations

### 1. **Use Environment Variables for Passwords**

```powershell
$Neo4jPassword = $env:NEO4J_PROD_PASSWORD

.\start-project.ps1 `
    -ProjectPath "E:\GitHub\CBC_AI" `
    -Neo4jUrl "bolt://prod-neo4j:7687" `
    -Neo4jPassword $Neo4jPassword
```

### 2. **Use TLS for Production**

```powershell
# Always use TLS in production
-Neo4jUrl "bolt+s://secure-neo4j:7687"
-QdrantUrl "https://secure-qdrant:6334"
```

### 3. **Network Security**

- Ensure firewall rules allow MCP Server container to reach external services
- Use VPN or private networks for sensitive data
- Don't expose services to public internet without authentication

---

## 🧪 Testing External Connections

### Test Qdrant Connection

```powershell
# HTTP API (port 6333)
Invoke-WebRequest -Uri "http://your-qdrant:6333/collections"

# Expected: JSON response with collections list
```

### Test Neo4j Connection

```powershell
# Use cypher-shell or Neo4j Browser
docker run --rm neo4j:5.15 cypher-shell `
    -a bolt://your-neo4j:7687 `
    -u neo4j `
    -p your-password `
    "RETURN 1"

# Expected: Returns 1
```

### Test Ollama Connection

```powershell
Invoke-WebRequest -Uri "http://your-ollama:11434/api/tags"

# Expected: JSON response with model list
```

---

## 📝 Complete Example

```powershell
# Production setup with all external services
.\start-project.ps1 `
    -ProjectPath "E:\GitHub\CBC_AI" `
    -ProjectName "cbcai-prod" `
    -QdrantUrl "https://qdrant-prod.company.com:6334" `
    -Neo4jUrl "bolt+s://neo4j-prod.company.com:7687" `
    -Neo4jUser "app-user" `
    -Neo4jPassword $env:NEO4J_PROD_PASSWORD `
    -OllamaUrl "https://ollama-gpu.company.com:11434" `
    -AutoIndex
```

**Output:**
```
========================================
Memory Code Agent - Project Startup
========================================

Project Path: E:\GitHub\CBC_AI
Project Name: cbcai-prod
...

Using external Qdrant: https://qdrant-prod.company.com:6334
Using external Neo4j: bolt+s://neo4j-prod.company.com:7687
Using external Ollama: https://ollama-gpu.company.com:11434

Access Points:
  MCP Server:      http://localhost:5098
  Qdrant:          https://qdrant-prod.company.com:6334 (external)
  Neo4j:           bolt+s://neo4j-prod.company.com:7687 (external, user: app-user)
  Ollama:          https://ollama-gpu.company.com:11434 (external)
```

---

## ✅ Summary

### Default (Containerized)
```powershell
.\start-project.ps1 -ProjectPath "E:\GitHub\CBC_AI"
```
- ✅ Simple, everything local
- ✅ No configuration needed
- ✅ Isolated per project

### External Services
```powershell
.\start-project.ps1 `
    -ProjectPath "E:\GitHub\CBC_AI" `
    -QdrantUrl "http://qdrant:6334" `
    -Neo4jUrl "bolt://neo4j:7687" `
    -Neo4jUser "user" `
    -Neo4jPassword "pass"
```
- ✅ Shared infrastructure
- ✅ Production-grade services
- ✅ Resource optimization
- ✅ Cloud-native deployments

**You now have full flexibility!** 🎉

