# Smart Search API

## 🧠 Overview

The **Smart Search API** (`/api/smartsearch`) automatically detects whether your query needs:
- **Graph-first** search (structural/relationship queries)
- **Semantic-first** search (natural language/concept queries)  
- **Hybrid** search (both in parallel)

Returns enriched results with both **semantic scores** AND **relationship data**.

---

## 🎯 Endpoint

```
POST /api/smartsearch
```

---

## 📋 Request

```json
{
  "query": "classes that implement IRepository",
  "context": "CBC_AI",
  "limit": 20,
  "offset": 0,
  "includeRelationships": true,
  "relationshipDepth": 1,
  "minimumScore": 0.5
}
```

### Parameters:

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `query` | string | ✅ Yes | - | Natural language query or graph pattern |
| `context` | string | ❌ No | null | Project context to search within |
| `limit` | int | ❌ No | 20 | Max results (1-100) |
| `offset` | int | ❌ No | 0 | Pagination offset |
| `includeRelationships` | bool | ❌ No | true | Include graph relationships |
| `relationshipDepth` | int | ❌ No | 1 | Relationship depth (1-3) |
| `minimumScore` | float | ❌ No | 0.5 | Min semantic score (0.0-1.0) |

---

## 📤 Response

```json
{
  "query": "classes that implement IRepository",
  "strategy": "graph-first",
  "results": [
    {
      "name": "UserRepository",
      "type": "Class",
      "content": "public class UserRepository : IRepository<User> { ... }",
      "filePath": "/workspace/CBC_AI/Data/UserRepository.cs",
      "lineNumber": 15,
      "score": 0.93,
      "semanticScore": 0.87,
      "graphScore": 0.95,
      "relationships": {
        "usedBy": ["UserController", "AuthService"],
        "dependencies": ["DbContext", "ILogger"]
      },
      "metadata": {
        "impactType": "implementation"
      }
    }
  ],
  "totalFound": 12,
  "hasMore": false,
  "processingTime": "00:00:00.234",
  "metadata": {
    "totalResults": 12,
    "returnedResults": 12,
    "offset": 0,
    "limit": 20
  }
}
```

---

## 🎨 Auto-Detection Examples

### **Graph-First Queries:**

```json
// Triggers: "implement", "implements"
{ "query": "classes that implement IRepository" }
→ Strategy: "graph-first"

// Triggers: "inherit", "extends"
{ "query": "classes that inherit from BaseController" }
→ Strategy: "graph-first"

// Triggers: "call", "calls"
{ "query": "methods that call SaveChangesAsync" }
→ Strategy: "graph-first"

// Triggers: "interface" + specific name
{ "query": "interface IUserService" }
→ Strategy: "graph-first"
```

### **Semantic-First Queries:**

```json
// Natural language / conceptual
{ "query": "How do we handle database errors?" }
→ Strategy: "semantic-first"

// Pattern discovery
{ "query": "error handling patterns" }
→ Strategy: "semantic-first"

// General concept
{ "query": "authentication implementation" }
→ Strategy: "semantic-first"
```

### **Hybrid Queries:**

```json
// Mix of specific + conceptual
{ "query": "UserService error handling" }
→ Strategy: "hybrid"

// One graph keyword but conceptual
{ "query": "how classes use dependency injection" }
→ Strategy: "hybrid"
```

---

## 🔍 Detection Keywords

### Graph Patterns (graph-first):
- `implement`, `implements`, `implementation`
- `inherit`, `inherits`, `inheritance`, `extends`
- `interface`
- `call`, `calls`, `calling`
- `use`, `uses`, `using`
- `depend`, `depends`, `dependency`
- `relationship`
- `that have`, `that has`
- `with attribute`, `with annotation`

### Hybrid Triggers:
- 1 graph keyword + conceptual question
- Specific class name + general concept

### Semantic Default:
- Natural language questions
- Concept-based queries
- "How", "What", "Where", "Show me"

---

## 📊 Scoring System

### **Combined Score:**
```
graph-first:    score = (graphScore * 0.7) + (semanticScore * 0.3)
semantic-first: score = (semanticScore * 0.7) + (graphScore * 0.3)
hybrid:         score = (graphScore + semanticScore) / 2
```

### **Score Components:**

| Score Type | Range | Description |
|------------|-------|-------------|
| `score` | 0.0-1.0 | Combined relevance score |
| `semanticScore` | 0.0-1.0 | Semantic similarity (Qdrant) |
| `graphScore` | 0.0-1.0 | Graph relevance (Neo4j) |

---

## 🔗 Relationships

When `includeRelationships: true`, each result includes:

```json
"relationships": {
  "usedBy": ["UserController", "AuthService", "ProfileService"],
  "dependencies": ["DbContext", "ILogger", "IMapper"]
}
```

### Relationship Types:

| Type | Description |
|------|-------------|
| `usedBy` | What uses this element |
| `dependencies` | What this element uses |

**Depth Control:**
- `relationshipDepth: 1` - Direct relationships only
- `relationshipDepth: 2` - Direct + 1 level
- `relationshipDepth: 3` - Direct + 2 levels

---

## 📖 Pagination

### **Basic Pagination:**

```json
// Page 1 (results 0-19)
{ "query": "...", "limit": 20, "offset": 0 }

// Page 2 (results 20-39)
{ "query": "...", "limit": 20, "offset": 20 }

// Page 3 (results 40-59)
{ "query": "...", "limit": 20, "offset": 40 }
```

### **Response Indicators:**

```json
{
  "totalFound": 45,
  "hasMore": true,  // More results available
  "metadata": {
    "offset": 20,
    "limit": 20,
    "returnedResults": 20
  }
}
```

---

## 🎯 Usage Examples

### **Example 1: Find Implementations**

```bash
curl -X POST http://localhost:5098/api/smartsearch \
  -H "Content-Type: application/json" \
  -d '{
    "query": "classes that implement IRepository",
    "context": "CBC_AI",
    "limit": 10
  }'
```

**Result:** Graph-first search, returns all repository implementations with relationships.

---

### **Example 2: Semantic Concept Search**

```bash
curl -X POST http://localhost:5098/api/smartsearch \
  -H "Content-Type: application/json" \
  -d '{
    "query": "How do we handle authentication errors?",
    "context": "CBC_AI",
    "minimumScore": 0.7
  }'
```

**Result:** Semantic-first search, finds error handling patterns with relationship context.

---

### **Example 3: Pagination**

```powershell
# First page
$body = @{
    query = "dependency injection patterns"
    context = "CBC_AI"
    limit = 20
    offset = 0
} | ConvertTo-Json

$response = Invoke-RestMethod -Uri http://localhost:5098/api/smartsearch `
    -Method POST -Body $body -ContentType 'application/json'

if ($response.hasMore) {
    # Get next page
    $body2 = @{
        query = "dependency injection patterns"
        context = "CBC_AI"
        limit = 20
        offset = 20
    } | ConvertTo-Json
    
    $response2 = Invoke-RestMethod -Uri http://localhost:5098/api/smartsearch `
        -Method POST -Body $body2 -ContentType 'application/json'
}
```

---

### **Example 4: Deep Relationships**

```json
{
  "query": "UserService",
  "context": "CBC_AI",
  "includeRelationships": true,
  "relationshipDepth": 2,
  "limit": 5
}
```

**Result:** Finds UserService with 2 levels of relationships (what uses it, what it uses, and their dependencies).

---

## 🚀 Query Suggestions

```
GET /api/smartsearch/suggest?query=error
```

Returns suggestions based on query:

```json
[
  "error handling patterns",
  "exception handling in services",
  "methods that throw exceptions"
]
```

---

## ⚡ Performance

### **Typical Response Times:**

| Strategy | Query Type | Time |
|----------|-----------|------|
| Graph-first | "classes that implement X" | 100-300ms |
| Semantic-first | "How do we handle Y?" | 200-400ms |
| Hybrid | Mixed queries | 300-600ms |

### **Optimization Tips:**

1. **Use specific queries** for graph-first routing (faster)
2. **Set higher minimumScore** to reduce result processing
3. **Disable relationships** if not needed (`includeRelationships: false`)
4. **Limit depth** for complex graphs (`relationshipDepth: 1`)

---

## 🎨 Integration with MCP

### **MCP Tool Definition:**

```json
{
  "name": "smartsearch",
  "description": "Search codebase with auto-detection of graph vs semantic strategy",
  "inputSchema": {
    "type": "object",
    "properties": {
      "query": {
        "type": "string",
        "description": "Natural language query or graph pattern"
      },
      "context": {
        "type": "string",
        "description": "Project context to search within"
      },
      "limit": {
        "type": "number",
        "default": 20
      }
    },
    "required": ["query"]
  }
}
```

### **MCP Usage:**

The AI in Cursor can call this automatically:

```
User: "Show me all classes that implement IRepository"
→ MCP calls: smartsearch({ query: "classes that implement IRepository", context: "CBC_AI" })
→ Returns: enriched results with code + relationships

User: "How do we handle database errors?"
→ MCP calls: smartsearch({ query: "How do we handle database errors?", context: "CBC_AI" })
→ Returns: error handling code + usage context
```

---

## ✅ Benefits

1. **No Strategy Decision** - Auto-detects best approach
2. **Enriched Results** - Both semantic scores AND relationships
3. **Pagination** - Handle large result sets
4. **Fast** - Optimized for each query type
5. **Flexible** - Works with natural language OR specific patterns
6. **Context-Aware** - Uses relationships to boost relevance

---

**Status:** ✅ Active and ready  
**Version:** 1.0  
**Endpoint:** `/api/smartsearch`  
**Last Updated:** 2025-11-22

