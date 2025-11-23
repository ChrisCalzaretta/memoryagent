# Neo4j Queries for Memory Code Agent

## Connection Details
- **URL**: http://localhost:7572
- **Username**: `neo4j`
- **Password**: `memoryagent`

## Top Queries to Explore Your Indexed Code

### 1. See All Razor/CSHTML Files
```cypher
// Find all Razor files
MATCH (n:CodeMemory)
WHERE n.file_path CONTAINS '.cshtml' OR n.file_path CONTAINS '.razor'
RETURN n.file_path, n.name, n.type, n.context
LIMIT 50
```

### 2. Count Files by Type
```cypher
// Count indexed files by extension
MATCH (n:CodeMemory)
WHERE n.file_path IS NOT NULL
WITH n.file_path as path
WITH CASE 
  WHEN path CONTAINS '.cshtml' THEN 'Razor/CSHTML'
  WHEN path CONTAINS '.razor' THEN 'Razor'
  WHEN path CONTAINS '.cs' AND NOT path CONTAINS '.cshtml' THEN 'C#'
  WHEN path CONTAINS '.bicep' THEN 'Bicep'
  WHEN path CONTAINS '.json' THEN 'JSON'
  WHEN path CONTAINS '.py' THEN 'Python'
  WHEN path CONTAINS '.md' THEN 'Markdown'
  WHEN path CONTAINS '.css' OR path CONTAINS '.scss' THEN 'CSS'
  ELSE 'Other'
END as fileType
RETURN fileType, COUNT(*) as count
ORDER BY count DESC
```

### 3. See All Code Memory Types
```cypher
// See what types of code elements are indexed
MATCH (n:CodeMemory)
RETURN n.type as ElementType, COUNT(*) as count
ORDER BY count DESC
```

### 4. Find Classes and Methods in Razor Files
```cypher
// Find classes and methods from Razor files
MATCH (file:CodeMemory)-[r:DEFINES|CONTAINS]->(element:CodeMemory)
WHERE file.file_path CONTAINS '.cshtml' OR file.file_path CONTAINS '.razor'
RETURN file.file_path, 
       type(r) as relationship, 
       element.name, 
       element.type
LIMIT 50
```

### 5. See Code Relationships
```cypher
// View how code elements are connected
MATCH (a:CodeMemory)-[r]->(b:CodeMemory)
RETURN a.name, 
       type(r) as relationship, 
       b.name, 
       a.file_path, 
       b.file_path
LIMIT 100
```

### 6. Find Specific File by Name
```cypher
// Find a specific file (replace 'YourFileName')
MATCH (n:CodeMemory)
WHERE n.file_path CONTAINS 'Index.cshtml'
RETURN n
LIMIT 20
```

### 7. See All Bicep Resources
```cypher
// Find all Bicep resources
MATCH (n:CodeMemory)
WHERE n.file_path CONTAINS '.bicep'
  AND n.metadata IS NOT NULL
RETURN n.name, n.file_path, n.metadata
LIMIT 50
```

### 8. Find Files in Specific Project
```cypher
// Find files in specific context (e.g., 'CBC_AI')
MATCH (n:CodeMemory)
WHERE n.context = 'CBC_AI'
  AND n.file_path CONTAINS '.cshtml'
RETURN n.file_path, n.name, n.type
LIMIT 50
```

### 9. See Method Calls (Dependencies)
```cypher
// Find method call relationships
MATCH (caller:CodeMemory)-[r:CALLS]->(callee:CodeMemory)
RETURN caller.name, 
       caller.file_path, 
       callee.name, 
       callee.file_path
LIMIT 50
```

### 10. Visualize a File's Structure
```cypher
// Visualize a file and all its elements
MATCH path = (file:CodeMemory)-[*1..2]-(element:CodeMemory)
WHERE file.file_path CONTAINS 'Index.cshtml'
RETURN path
LIMIT 100
```

### 11. Find CSS/Style Elements in Razor Files
```cypher
// Find style tags and inline styles extracted from Razor files
MATCH (n:CodeMemory)
WHERE (n.name CONTAINS 'StyleTag' OR n.name CONTAINS 'InlineStyle')
  AND n.file_path CONTAINS '.cshtml'
RETURN n.name, n.file_path, n.content
LIMIT 20
```

### 12. See All Contexts (Projects)
```cypher
// See all indexed contexts/projects
MATCH (n:CodeMemory)
RETURN DISTINCT n.context, COUNT(*) as elements
ORDER BY elements DESC
```

### 13. Graph Overview - All Node Types
```cypher
// See all node and relationship types in the database
CALL db.schema.visualization()
```

### 14. Find Large/Complex Files
```cypher
// Find files with many elements (complex files)
MATCH (file:CodeMemory)-[r]->(element:CodeMemory)
WHERE file.type = 'File'
WITH file, COUNT(r) as elementCount
WHERE elementCount > 10
RETURN file.file_path, elementCount
ORDER BY elementCount DESC
LIMIT 20
```

### 15. Search by Content
```cypher
// Find code elements containing specific text
MATCH (n:CodeMemory)
WHERE n.content CONTAINS 'HttpClient'
  OR n.name CONTAINS 'HttpClient'
RETURN n.name, n.type, n.file_path
LIMIT 30
```

## Quick Checks

### Total Nodes
```cypher
MATCH (n:CodeMemory)
RETURN COUNT(n) as TotalNodes
```

### Total Relationships
```cypher
MATCH ()-[r]->()
RETURN type(r) as RelationType, COUNT(r) as Count
ORDER BY Count DESC
```

### Recent Indexed Files
```cypher
MATCH (n:CodeMemory)
WHERE n.indexed_at IS NOT NULL
RETURN n.file_path, n.indexed_at
ORDER BY n.indexed_at DESC
LIMIT 20
```

## Advanced Queries

### Find Circular Dependencies
```cypher
// Find circular dependencies (A calls B, B calls A)
MATCH (a:CodeMemory)-[:CALLS]->(b:CodeMemory)-[:CALLS]->(a)
WHERE a.name <> b.name
RETURN DISTINCT a.name, a.file_path, b.name, b.file_path
LIMIT 50
```

### Impact Analysis (What uses this?)
```cypher
// Find what depends on a specific class/method
MATCH (target:CodeMemory {name: 'YourClassName'})<-[r]-(dependent:CodeMemory)
RETURN dependent.name, type(r), dependent.file_path
LIMIT 50
```

### Find All Forms in Razor Files
```cypher
// Find HTML forms extracted from Razor files
MATCH (n:CodeMemory)
WHERE n.name CONTAINS 'Form_'
  AND (n.file_path CONTAINS '.cshtml' OR n.file_path CONTAINS '.razor')
RETURN n.name, n.file_path, n.line_number
LIMIT 30
```

## Tips

1. **Case Sensitivity**: Neo4j Cypher is case-sensitive for property names but not for keywords
2. **Limit Results**: Always use `LIMIT` to avoid overwhelming results
3. **Visualizations**: Click the graph view in Neo4j Browser for visual representation
4. **Full-Text Search**: For better content search, consider using full-text indexes
5. **Performance**: Add indexes on frequently queried properties:
   ```cypher
   CREATE INDEX code_memory_file_path IF NOT EXISTS
   FOR (n:CodeMemory) ON (n.file_path)
   ```

## Understanding the Data Model

- **CodeMemory Node**: Represents a code element (File, Class, Method, Pattern, etc.)
- **Relationships**: 
  - `DEFINES`: File defines a class/method
  - `CALLS`: Method calls another method
  - `IMPLEMENTS`: Class implements interface
  - `INHERITS`: Class inherits from base class
  - `USES`: Uses/references another element
  - `CONTAINS`: Contains child elements

## Note on Embeddings

**Embeddings are stored in Qdrant**, not Neo4j. Neo4j stores:
- Code structure (classes, methods, files)
- Relationships between code elements
- Metadata

For semantic search using embeddings, use the API endpoints:
- `/api/query` - Semantic search
- `/api/smartsearch` - Hybrid search (graph + semantic)

To see embedding data, use:
```bash
# Qdrant Dashboard
http://localhost:6431/dashboard

# Or query via API
$body = @{query='your search';context='CBC_AI'} | ConvertTo-Json
Invoke-RestMethod -Uri http://localhost:5098/api/query -Method POST -Body $body -ContentType 'application/json'
```

