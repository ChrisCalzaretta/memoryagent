# Testing Resilient Embeddings - Quick Guide

## ✅ **DEPLOYMENT COMPLETE!**

All changes have been:
- ✅ Implemented
- ✅ Built successfully
- ✅ Deployed to Docker containers
- ✅ Containers are running

---

## 🧪 **HOW TO TEST**

### **Test 1: Normal Operation (Should work as before)**

```powershell
$body = @{
    path = "E:\GitHub\AgentTrader\infrastructure\config.py"
    context = "AgentTrader"
} | ConvertTo-Json

$result = Invoke-RestMethod -Uri "http://localhost:5000/api/index/file" `
    -Method POST -Body $body -ContentType "application/json" -TimeoutSec 120

Write-Host "Success: $($result.success)"
Write-Host "Classes: $($result.classesFound)"
Write-Host "Methods: $($result.methodsFound)"
Write-Host "Patterns: $($result.patternsDetected)"
```

**Expected:** All files index successfully, embeddings work

---

### **Test 2: Individual Item Failure**

Create a file with one very long function (>5000 chars):

```python
# test_long_function.py
def extremely_long_function():
    # ... paste 5000+ characters of code ...
    pass

def normal_function():
    return "hello"
```

Then index it:
```powershell
$body = @{ path = "E:\path\to\test_long_function.py"; context = "test" } | ConvertTo-Json
$result = Invoke-RestMethod -Uri "http://localhost:5000/api/index/file" -Method POST -Body $body -ContentType "application/json"
```

**Expected:** 
- File succeeds ✅
- `normal_function` gets embedded ✅
- Long function gets zero vector (warning logged) ⚠️
- Both functions stored in Neo4j ✅

---

### **Test 3: Ollama Down (Graceful Degradation)**

```powershell
# Stop Ollama
docker stop memory-agent-ollama

# Try to index a file
$body = @{ path = "E:\GitHub\AgentTrader\main.py"; context = "AgentTrader" } | ConvertTo-Json
$result = Invoke-RestMethod -Uri "http://localhost:5000/api/index/file" -Method POST -Body $body -ContentType "application/json"

Write-Host "Success: $($result.success)"  # Should be true!
Write-Host "Errors: $($result.errors)"     # Should mention embeddings failed

# Restart Ollama
docker start memory-agent-ollama
```

**Expected:**
- File indexing succeeds ✅
- Error message: "Warning: Semantic search unavailable - embedding generation failed"
- All code elements in Neo4j ✅
- No embeddings in Qdrant ❌
- Graph relationships work! ✅

---

### **Test 4: Check Logs for Resilience**

```powershell
# Watch logs in real-time
docker logs memory-agent-server --follow

# Then index a file in another terminal
# Look for these log messages:
```

**Look for:**

**Normal operation:**
```
Successfully generated 125 embeddings for /workspace/myfile.py
```

**Individual item failure:**
```
WARN: Failed to generate embedding after 3 retries (text length: 2500). Using zero vector to allow batch to continue.
WARN: Skipping Method calculate_total - zero vector (embedding failed)
```

**Complete embedding failure:**
```
WARN: Embedding generation failed for /workspace/myfile.py. Continuing with Neo4j-only storage
INFO: Skipping Qdrant storage for /workspace/myfile.py (no embeddings)
```

---

## 📊 **VERIFY IN NEO4J**

```cypher
// Check that file was indexed despite embedding failures
MATCH (f:File {path: "/workspace/myfile.py"})
RETURN f

// Check that relationships exist
MATCH (f:File)-[r]->(n)
WHERE f.path = "/workspace/myfile.py"
RETURN type(r), count(*) as count
```

**Expected:** File node exists, relationships exist (even if embeddings failed)

---

## 📊 **VERIFY IN QDRANT**

```powershell
# Check collection exists
Invoke-RestMethod -Uri "http://localhost:6333/collections/agenttrader_classes" -Method GET

# Check points count
Invoke-RestMethod -Uri "http://localhost:6333/collections/agenttrader_classes" -Method GET | 
    Select-Object -ExpandProperty result | 
    Select-Object -ExpandProperty points_count

# Should match number of successfully embedded items (not total items)
```

---

## 🎯 **WHAT TO LOOK FOR**

### **✅ SUCCESS INDICATORS:**

1. **File indexing always succeeds** (even if embeddings fail)
2. **Neo4j graph always complete** (relationships work)
3. **Partial embedding failures don't kill batch**
4. **Warning logs for failed items** (not errors)
5. **Zero vectors automatically skipped** in Qdrant

### **❌ REGRESSION INDICATORS:**

1. File indexing fails when embeddings fail
2. Batch fails if one item fails
3. No data in Neo4j after embedding failure
4. Timeout errors with slow Ollama

---

## 🔍 **MONITORING COMMANDS**

### **Check container health:**
```powershell
docker compose -f docker-compose-shared.yml ps
```

### **Watch server logs:**
```powershell
docker logs memory-agent-server --follow
```

### **Check embedding service:**
```powershell
# Test Ollama directly
Invoke-RestMethod -Uri "http://localhost:11434/api/tags" -Method GET

# Check if model is loaded
docker exec memory-agent-ollama ollama list
```

### **Check Neo4j:**
```powershell
# Open browser to http://localhost:7474
# Username: neo4j
# Password: memoryagent
```

### **Check Qdrant:**
```powershell
# Open browser to http://localhost:6333/dashboard
# Or use API:
Invoke-RestMethod -Uri "http://localhost:6333/collections" -Method GET
```

---

## 🚀 **QUICK SMOKE TEST**

```powershell
Write-Host "🧪 Running smoke test..."

# Test 1: Index a Python file
$body = @{
    path = "E:\GitHub\AgentTrader\test_patterns.py"
    context = "AgentTrader"
} | ConvertTo-Json

try {
    $result = Invoke-RestMethod -Uri "http://localhost:5000/api/index/file" `
        -Method POST -Body $body -ContentType "application/json" -TimeoutSec 60
    
    if ($result.success) {
        Write-Host "✅ TEST PASSED!"
        Write-Host "   Files: $($result.filesIndexed)"
        Write-Host "   Classes: $($result.classesFound)"
        Write-Host "   Methods: $($result.methodsFound)"
        Write-Host "   Patterns: $($result.patternsDetected)"
    } else {
        Write-Host "❌ TEST FAILED!"
        Write-Host "   Errors: $($result.errors -join ', ')"
    }
} catch {
    Write-Host "❌ REQUEST FAILED: $($_.Exception.Message)"
}
```

---

## 📝 **EXPECTED BEHAVIOR SUMMARY**

| Scenario | Before | After |
|----------|--------|-------|
| **1 item fails in batch** | ❌ All lost | ✅ 31 of 32 succeed |
| **All embeddings fail** | ❌ File fails | ✅ Neo4j-only mode |
| **Ollama slow (10 min)** | ✅ Works | ✅ Works (no timeout) |
| **Network hiccup** | ✅ Retries | ✅ Retries (same) |
| **Malformed input** | ❌ Batch fails | ✅ Item skipped |

---

## ✅ **RETRY LOGIC CONFIRMATION**

Each embedding attempt STILL retries:
1. **Attempt 1** → Fails → Wait 2s
2. **Attempt 2** → Fails → Wait 4s
3. **Attempt 3** → Fails → Wait 8s
4. **Attempt 4** → Fails → Use zero vector

Total: 3 retries with exponential backoff ✅

---

## 🎓 **KEY CHANGES DEPLOYED**

1. **Individual item error handling** - One failure doesn't kill batch
2. **Zero vector placeholders** - Failed items get placeholder
3. **Automatic skipping** - Zero vectors not stored in Qdrant
4. **Graceful degradation** - Neo4j-only mode when embeddings fail
5. **NO timeout limits** - Ollama can take as long as needed

---

**Your system is now highly resilient!** 🚀

Test it and verify the new behavior works as expected.


