# Search Architecture Explained

## What Does `search` / `query` Do?

Both `search` and `query` are the **same tool** (search is an alias). Here's what they actually search:

### 🔍 Searches **Qdrant ONLY** (Vector Database)

**How it works:**
1. Takes your natural language question (e.g., "How do we handle errors?")
2. Converts it to a 1024-dimension embedding using Ollama (mxbai-embed-large)
3. Searches Qdrant vector database for **semantically similar code**
4. Returns code snippets ranked by similarity score

**What you get back:**
- Actual code snippets (methods, classes, properties)
- File paths
- Similarity scores (0.0 to 1.0)
- Context information

**Example:**
```
Query: "How do we handle database errors?"

Results:
1. (Score: 0.92) DataPrepPlatform.Data.ProjectDbContext.SaveChangesAsync
   - Catches DbUpdateException, logs errors, returns Result<T>
   
2. (Score: 0.87) ErrorHandlingMiddleware.InvokeAsync
   - Global exception handler, catches all exceptions
   
3. (Score: 0.84) RepositoryBase.ExecuteWithRetry
   - Retry logic for transient database failures
```

---

## Database Roles

### Qdrant (Vector Database)
**Purpose:** Semantic/meaning-based search

**Stores:**
- Code embeddings (vector representations)
- Code snippets (actual text)
- Metadata (file path, type, context)

**Used by:**
- `query` / `search` - Semantic code search

**Strengths:**
- "Find similar code" - even if different variable names
- Natural language questions
- Pattern discovery
- Example finding

---

### Neo4j (Graph Database)
**Purpose:** Relationship/dependency tracking

**Stores:**
- Code elements (classes, methods, properties) as **nodes**
- Relationships as **edges**:
  - CALLS (method → method)
  - INJECTS (class → dependency)
  - INHERITS (class → base class)
  - IMPLEMENTS (class → interface)
  - HASTYPE (property → type)
  - ACCEPTSTYPE (method → parameter type)
  - RETURNSTYPE (method → return type)
  - IMPORTS (file → namespace)
  - HASATTRIBUTE (element → attribute)
  - USESGENERIC (class → generic type)
  - THROWS (method → exception)
  - CATCHES (method → exception)
  - DEFINES (context → element)

**Used by:**
- `impact_analysis` - "What breaks if I change X?"
- `dependency_chain` - "What does X depend on?"
- `find_circular_dependencies` - "Are there circular refs?"

**Strengths:**
- Precise relationships
- Dependency traversal
- Impact analysis
- Architectural queries

---

## Search Examples by Database

### Use Qdrant (via `search`/`query`) When:

❓ **"Find similar patterns"**
- "Show me all error handling patterns"
- "Find authentication code"
- "How do we validate input?"

❓ **"Natural language questions"**
- "Where do we call Azure OpenAI?"
- "How is dependency injection configured?"
- "Find all API endpoints"

❓ **"Code by meaning, not name"**
- Finds code even if variable names differ
- Discovers similar implementations
- Pattern recognition

---

### Use Neo4j (via `impact_analysis`, etc.) When:

🔗 **"What depends on this?"**
```cypher
impact_analysis: "DataPrepPlatform.Core.Services.UserService"

Returns:
- All classes that inject UserService
- All methods that call UserService methods
- All files that import the UserService namespace
```

🔗 **"Full dependency tree"**
```cypher
dependency_chain: "ProjectDbContext"

Returns:
- ProjectDbContext → DbContext (inherits)
- ProjectDbContext → ProjectRepository (injected into)
- ProjectRepository → ProjectService (injected into)
- ProjectService → ProjectController (injected into)
```

🔗 **"Circular reference detection"**
```cypher
find_circular_dependencies

Returns:
- A → B → C → A (circular!)
```

---

## Combined Search Strategy

For comprehensive analysis, **use both**:

### Example: Refactoring UserService

**Step 1:** Use `search` to understand current implementation
```
search: "How is UserService implemented?"
```
→ Returns all UserService methods, patterns, and similar code

**Step 2:** Use `impact_analysis` to see what breaks
```
impact_analysis: "DataPrepPlatform.Core.Services.UserService"
```
→ Returns all dependent classes/methods

**Step 3:** Use `dependency_chain` to see full context
```
dependency_chain: "UserService"
```
→ Returns complete dependency tree

---

## Data Flow

### When you index a file:

```
Code File
   ↓
Roslyn Parser extracts:
   ├─ Code elements (classes, methods, etc.)
   ├─ Relationships (calls, injections, etc.)
   └─ Code snippets
   ↓
Split into TWO paths:
   ├─ Qdrant: Store embeddings + snippets (for search)
   └─ Neo4j: Store nodes + relationships (for dependencies)
```

### When you search:

```
Natural Language Query
   ↓
Embedding Service (Ollama)
   ↓
Vector (1024 dimensions)
   ↓
Qdrant similarity search
   ↓
Code snippets ranked by similarity
```

### When you analyze impact:

```
Class Name
   ↓
Neo4j graph traversal (follow edges)
   ↓
All connected nodes
   ↓
List of impacted classes/methods
```

---

## Current Implementation

### `search` / `query` Tool
```csharp
// Only searches Qdrant
private async Task<McpToolResult> QueryToolAsync(...)
{
    var result = await _indexingService.QueryAsync(query, context, limit, minimumScore, ct);
    // QueryAsync:
    // 1. Generates embedding for query
    // 2. Searches Qdrant vector DB
    // 3. Returns similar code snippets
}
```

### `impact_analysis` Tool
```csharp
// Only searches Neo4j
private async Task<McpToolResult> ImpactAnalysisToolAsync(...)
{
    var impacted = await _graphService.GetImpactAnalysisAsync(className, ct);
    // GetImpactAnalysisAsync:
    // 1. Finds class node in Neo4j
    // 2. Traverses incoming edges (CALLS, INJECTS, etc.)
    // 3. Returns all dependent nodes
}
```

---

## Summary

| Tool | Database | Purpose | Query Type |
|------|----------|---------|------------|
| `search` / `query` | **Qdrant** | Semantic search | "Find code that **means** X" |
| `impact_analysis` | **Neo4j** | Dependency impact | "What **depends on** X?" |
| `dependency_chain` | **Neo4j** | Dependency tree | "What does X **depend on**?" |
| `find_circular_dependencies` | **Neo4j** | Circular refs | "Any **circular** dependencies?" |

**Both databases are updated together** when you index code, but they serve different search purposes! 🚀

