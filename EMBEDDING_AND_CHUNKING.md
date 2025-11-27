# 🧠 Embeddings & Descriptions in Qdrant - How It Works

## **TL;DR:**

**YES!** We store rich metadata with each chunk, but the `content` field is the **ACTUAL CODE**, not a generated description.

The embedding is generated from the code itself, and we store extensive metadata alongside it.

---

## **What We Store in Qdrant:**

### **For Each Code Chunk (Point), We Store:**

1. **Vector (Embedding)** - 1024-dimensional embedding from Ollama
2. **Payload (Metadata)** - All the searchable fields

---

## **The Payload Structure:**

### **Common Fields (All Chunks):**

```json
{
  "name": "McpService",                    // Class/method/file name
  "content": "public class McpService...", // ← FULL CODE (not summary!)
  "file_path": "/workspace/MemoryAgent/Services/McpService.cs",
  "context": "MemoryAgent",                // Which workspace
  "line_number": 42,
  "indexed_at": "2025-11-27T..."
}
```

### **For Files:**

```json
{
  "name": "McpService.cs",
  "content": "using System;\nusing...",     // ← Entire file content (up to 2000 chars for non-C#)
  "file_path": "/workspace/...",
  "context": "MemoryAgent",
  "line_number": 0,
  "indexed_at": "2025-11-27T...",
  
  // Metadata:
  "size": 15000,
  "language": "csharp",
  "last_modified": "2025-11-27T...",
  "line_count": 500
}
```

### **For Classes:**

```json
{
  "name": "MemoryAgent.Server.Services.McpService",
  "content": "public class McpService {...}",  // ← FULL CLASS CODE
  "file_path": "/workspace/...",
  "context": "MemoryAgent",
  "line_number": 15,
  
  // Rich Metadata:
  "namespace": "MemoryAgent.Server.Services",
  "is_abstract": false,
  "is_static": false,
  "is_sealed": false,
  "access_modifier": "public",
  "language": "csharp",
  "layer": "Application",                   // Application/Domain/Infrastructure
  "bounded_context": "Server",
  
  // Class Metrics:
  "lines_of_code": 500,
  "method_count": 25,
  "property_count": 5,
  "field_count": 3,
  "is_god_class": false,                    // Code smell detection!
  
  // API Visibility:
  "is_public_api": true,
  "is_internal": false,
  
  // Framework Detection:
  "framework": "aspnet-core",               // If Controller
  "chunk_type": "controller"
}
```

### **For Methods:**

```json
{
  "name": "CallToolAsync",
  "content": "public async Task<McpToolResult> CallToolAsync(...) {...}",  // ← FULL METHOD CODE
  "file_path": "/workspace/...",
  "context": "MemoryAgent",
  "line_number": 551,
  
  // Complexity Metrics:
  "cyclomatic_complexity": 15,              // How many paths?
  "cognitive_complexity": 8,                // How hard to understand?
  "lines_of_code": 50,
  "nesting_depth": 3,
  
  // Code Smells:
  "code_smells": ["LongMethod", "HighComplexity"],
  
  // Behavior Analysis:
  "is_async": true,
  "returns_task": true,
  "exception_types": ["InvalidOperationException", "ArgumentException"],
  "database_call_count": 2,
  "has_http_calls": true,
  "has_logging": true,
  
  // API Characteristics:
  "is_public_api": true,
  "is_test_method": false,
  "access_modifier": "public",
  
  // Method Signature:
  "return_type": "Task<McpToolResult>",
  "parameter_count": 2,
  "parameters": "McpToolCall toolCall, CancellationToken cancellationToken"
}
```

### **For Patterns:**

```json
{
  "name": "Caching Pattern in UserService.GetUserAsync",
  "content": "await _cache.GetOrCreateAsync(...)",  // ← Pattern code
  "file_path": "/workspace/...",
  "context": "MemoryAgent",
  "line_number": 100,
  
  // Pattern Metadata:
  "pattern_type": "Caching",
  "pattern_category": "Performance",
  "implementation": "IMemoryCache with expiration",
  "best_practice": "Azure Cache for Redis best practices",
  "azure_url": "https://learn.microsoft.com/...",
  "confidence": 0.95,
  "language": "csharp",
  "is_positive_pattern": true,              // Good implementation vs anti-pattern
  "detected_at": "2025-11-27T..."
}
```

---

## **How Embeddings Are Generated:**

### **Step 1: Extract Code Chunks**

```csharp
// RoslynParser.cs - Line 213
Content = classDecl.ToString()  // Full class code
```

### **Step 2: Generate Embedding**

```csharp
// IndexingService.cs - Line 98-99
var textsToEmbed = parseResult.CodeElements.Select(e => e.Content).ToList();
var embeddings = await _embeddingService.GenerateEmbeddingsAsync(textsToEmbed, ct);
```

**What gets embedded:** The **actual code**, not a summary!

**Ollama Model:** `mxbai-embed-large` (1024 dimensions)

### **Step 3: Store in Qdrant**

```csharp
// VectorService.cs - Line 192-197
var point = new {
    id = Guid.NewGuid().ToString(),
    vector = memory.Embedding,      // 1024-dim vector
    payload = payload               // All metadata + full code
};
```

---

## **Why Store Full Code Instead of Summaries?**

### **Advantages:**

1. ✅ **Exact Code Available** - No need to go back to source file
2. ✅ **Semantic Search** - Embedding captures meaning, not just keywords
3. ✅ **Context Preserved** - Full code shows actual implementation
4. ✅ **Pattern Detection** - Can analyze code structure
5. ✅ **No Information Loss** - Original code is searchable

### **Example Query:**

**User asks:** "How do we handle caching?"

**Semantic Search:**
1. Generate embedding for query: "How do we handle caching?"
2. Find similar embeddings in Qdrant (cosine similarity)
3. Returns chunks with high similarity:
   - Classes using `IMemoryCache`
   - Methods with caching patterns
   - Pattern detections for caching

**Result:**
```json
{
  "name": "UserService.GetUserAsync",
  "content": "public async Task<User> GetUserAsync(int id) {\n    return await _cache.GetOrCreateAsync($\"user_{id}\", async entry => {\n        entry.SlidingExpiration = TimeSpan.FromMinutes(5);\n        return await _db.Users.FindAsync(id);\n    });\n}",
  "score": 0.92,
  "metadata": {
    "pattern_type": "Caching",
    "has_expiration": true
  }
}
```

---

## **Special Cases:**

### **Large Files (Non-C#):**

```csharp
// JavaScriptParser.cs - Line 34
Content = content.Length > 2000 
    ? content.Substring(0, 2000) + "..."  // Truncate large files
    : content
```

For JS/TS/VB/Dockerfile files > 2000 chars, we truncate to keep embedding size reasonable.

**C# files:** No truncation! Full code stored (Roslyn parses into chunks).

---

## **Pattern Embeddings (Special):**

For patterns, we create a **descriptive embedding**:

```csharp
// PatternIndexingService.cs - Line 59
var embeddingText = $"{pattern.Type}: {pattern.BestPractice}\n{pattern.Implementation}\n{pattern.Content}";
```

**Example:**
```
"Caching: Azure Cache for Redis best practices
IMemoryCache with expiration
await _cache.GetOrCreateAsync(...)"
```

This makes patterns searchable by type, best practice, AND implementation!

---

## **How Search Works:**

### **Query Flow:**

```
User: "@memory search for error handling"
    ↓
1. Generate embedding for "error handling"
    ↓
2. Search Qdrant collections:
   - memoryagent_files
   - memoryagent_classes
   - memoryagent_methods
   - memoryagent_patterns
    ↓
3. Find similar embeddings (cosine similarity > 0.7)
    ↓
4. Return results with:
   - Full code content
   - Metadata (complexity, patterns, etc.)
   - Similarity score
   - File path & line number
```

---

## **Summary:**

| Question | Answer |
|----------|--------|
| **Is there a description?** | No - we store the **actual code** |
| **What is embedded?** | The full code content |
| **What metadata is stored?** | LOTS! Complexity, patterns, frameworks, metrics |
| **Can I get the code back?** | Yes! It's in the `content` field |
| **Why not use summaries?** | Code itself is more accurate & useful |
| **How is it searchable?** | Semantic embeddings capture meaning |

---

## **Benefits of This Approach:**

✅ **Semantic Understanding** - Embeddings understand code meaning, not just syntax  
✅ **Rich Metadata** - Extensive metadata for filtering & analysis  
✅ **Full Context** - Actual code available for review  
✅ **Pattern Detection** - Automatic detection of coding patterns  
✅ **Complexity Analysis** - Metrics for code quality  
✅ **Framework Awareness** - Knows ASP.NET, EF Core, etc.  
✅ **DDD Support** - Bounded context & layer detection  

---

**The system is optimized for CODE INTELLIGENCE, not just text search!** 🧠

