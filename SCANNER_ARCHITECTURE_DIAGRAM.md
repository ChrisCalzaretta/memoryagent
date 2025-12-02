# Scanner Architecture - How It All Fits Together

## 🏗️ **System Architecture**

```
┌─────────────────────────────────────────────────────────────────┐
│                         USER / AI AGENT                          │
│                    (Cursor IDE, CLI, API)                        │
└──────────────────────────────┬──────────────────────────────────┘
                               │
                               │ MCP Protocol
                               ▼
┌─────────────────────────────────────────────────────────────────┐
│                        MCP SERVICE                               │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │  MCP TOOLS (Exposed to AI):                             │   │
│  │  • index_file                                            │   │
│  │  • analyze_python_quality ⬅️ NEW!                       │   │
│  │  • validate_security                                     │   │
│  │  • get_recommendations                                   │   │
│  │  • validate_pattern_quality                              │   │
│  │  • analyze_code_complexity                               │   │
│  └──────────────────────────────────────────────────────────┘   │
└──────────────────────────────┬──────────────────────────────────┘
                               │
                               │ Service Layer
                               ▼
┌─────────────────────────────────────────────────────────────────┐
│                      SCANNER SERVICES                            │
│                                                                  │
│  ┌─────────────┐  ┌─────────────┐  ┌──────────────────┐        │
│  │  SEMGREP    │  │   RUFF      │  │  BANDIT          │        │
│  │  (Security) │  │  (Quality)  │  │  (Security)      │        │
│  │             │  │             │  │                  │        │
│  │  • OWASP    │  │  • PEP 8    │  │  • Hardcoded     │        │
│  │  • CWE      │  │  • Auto-fix │  │    passwords     │        │
│  │  • All langs│  │  • Fast     │  │  • SQL inject    │        │
│  └─────────────┘  └─────────────┘  └──────────────────┘        │
│                                                                  │
│  ┌─────────────┐  ┌─────────────┐  ┌──────────────────┐        │
│  │   MYPY      │  │   RADON     │  │  SAFETY          │        │
│  │  (Types)    │  │ (Complexity)│  │  (Dependencies)  │        │
│  │             │  │             │  │                  │        │
│  │  • Type     │  │  • Cyclo CC │  │  • CVE check     │        │
│  │    hints    │  │  • Maint IX │  │  • PyPI vulns    │        │
│  │  • Static   │  │  • Halstead │  │  • Licenses      │        │
│  └─────────────┘  └─────────────┘  └──────────────────┘        │
│                                                                  │
│  ┌─────────────┐  ┌─────────────┐  ┌──────────────────┐        │
│  │   DOC8      │  │  VULTURE    │  │  CUSTOM          │        │
│  │  (Docs)     │  │ (Dead Code) │  │  (Your rules)    │        │
│  └─────────────┘  └─────────────┘  └──────────────────┘        │
└──────────────────────────────┬──────────────────────────────────┘
                               │
                               │ Results Processing
                               ▼
┌─────────────────────────────────────────────────────────────────┐
│              RECOMMENDATION ENGINE                               │
│                                                                  │
│  1. Aggregate results from all scanners                         │
│  2. Calculate scores (quality, security, complexity)            │
│  3. Deduplicate findings                                        │
│  4. Prioritize issues (Critical → Low)                          │
│  5. Generate actionable recommendations                         │
│  6. Provide auto-fix commands                                   │
│  7. Track trends over time                                      │
└──────────────────────────────┬──────────────────────────────────┘
                               │
                               │ Storage
                               ▼
┌────────────────────┐    ┌────────────────────┐
│      NEO4J         │    │      QDRANT        │
│  (Graph Database)  │    │  (Vector Search)   │
│                    │    │                    │
│  • Pattern nodes   │    │  • Embeddings      │
│  • Relationships   │    │  • Semantic search │
│  • Finding history │    │  • Similar issues  │
└────────────────────┘    └────────────────────┘
```

---

## 🔄 **TWO INTEGRATION MODES**

### **Mode 1: Auto-Run During Indexing**

```
File Change
    ↓
Auto-Reindex Service
    ↓
IndexingService.IndexFileAsync()
    ↓
┌───────────────────────────────┐
│ 1. Parse with AST Parser      │
│ 2. Detect Patterns            │
│ 3. Run Semgrep (security)     │ ⬅️ EXISTING
│ 4. Run Python Quality Suite   │ ⬅️ CAN ADD
│    • Ruff                     │
│    • Bandit                   │
│    • Mypy                     │
│    • Radon                    │
└───────────────────────────────┘
    ↓
Store Results → Neo4j + Qdrant
```

**Pros:** Automatic, always up-to-date  
**Cons:** Adds time to indexing

---

### **Mode 2: On-Demand via MCP Tool**

```
User/AI Request
    ↓
MCP Tool: "analyze_python_quality"
    ↓
PythonCodeQualityService
    ↓
Run All Scanners in Parallel
    ↓
Generate Report + Recommendations
    ↓
Return to User
```

**Pros:** Fast indexing, detailed analysis on-demand  
**Cons:** Manual invocation required

---

### **Mode 3: BOTH! (Recommended)**

```
┌─────────────────────────────┐
│  Auto-Run: Basic scans      │
│  • Security (Bandit)        │
│  • Critical issues only     │
└─────────────────────────────┘

┌─────────────────────────────┐
│  On-Demand: Full analysis   │
│  • All quality metrics      │
│  • Detailed recommendations │
│  • Auto-fix suggestions     │
└─────────────────────────────┘
```

---

## 🎯 **EXAMPLE: What You Could Call**

### **Current (Already Working):**
```bash
# Via MCP
validate_security(context="AgentTrader")
validate_best_practices(context="AgentTrader")  
validate_pattern_quality(pattern_id="some_pattern")
analyze_code_complexity(filePath="MyClass.cs")
```

### **With Python Scanners Added:**
```bash
# New MCP tools
analyze_python_quality(path="E:\GitHub\AgentTrader\main.py")
scan_python_security(path="E:\GitHub\AgentTrader")
check_python_types(path="E:\GitHub\AgentTrader")
analyze_python_complexity(path="E:\GitHub\AgentTrader")
validate_python_docs(path="E:\GitHub\AgentTrader\docs")

# Returns:
{
  "overallScore": 8.2,
  "codeQuality": 8.5,
  "security": 9.0,
  "typesSafety": 7.5,
  "complexity": 6.8,
  "recommendations": [
    {
      "priority": "High",
      "title": "Fix 2 security issues",
      "autoFix": "Run: ruff check --fix main.py"
    }
  ]
}
```

---

## 🚀 **WHAT MAKES THIS POWERFUL**

### **1. Unified Scoring**
- Single quality score (0-10) across all dimensions
- Comparable across projects
- Track improvements over time

### **2. Actionable Recommendations**
- Not just "here's what's wrong"
- Specific actions: "Run this command"
- Prioritized by impact
- Include examples

### **3. Multi-Tool Aggregation**
- Deduplicates findings across tools
- Cross-references issues
- Provides context from multiple perspectives

### **4. Historical Tracking**
```cypher
// Neo4j query
MATCH (f:File {path: "main.py"})-[r:HAS_SCAN]->(s:ScanResult)
WHERE s.timestamp > datetime() - duration('P7D')
RETURN s.timestamp, s.quality_score, s.security_score
ORDER BY s.timestamp
```

This shows quality trends over time! 📈

---

## 💡 **YOUR SPECIFIC USE CASE**

### **For RST Documentation Scanning:**

```csharp
public class RstDocumentationService : IRstDocumentationService
{
    public async Task<RstReport> ValidateDocsAsync(string docsPath)
    {
        // Run doc8
        var doc8Output = await RunCommand($"doc8 {docsPath} --quiet");
        
        // Run rst-lint
        var rstFiles = Directory.GetFiles(docsPath, "*.rst", SearchOption.AllRecursive);
        var lintResults = new List<RstLintResult>();
        
        foreach (var file in rstFiles)
        {
            var lintOutput = await RunCommand($"restructuredtext-lint {file}");
            lintResults.Add(ParseRstLint(file, lintOutput));
        }

        return new RstReport
        {
            Doc8Issues = ParseDoc8(doc8Output),
            LintIssues = lintResults,
            DocumentationQualityScore = CalculateDocScore(lintResults),
            Recommendations = new[]
            {
                new DocRecommendation
                {
                    Title = "Fix RST Formatting Issues",
                    Description = $"Found {lintResults.Sum(r => r.ErrorCount)} RST errors",
                    Impact = "Ensures Sphinx documentation builds successfully",
                    Files = lintResults.Where(r => r.ErrorCount > 0)
                        .Select(r => r.FilePath).ToList()
                }
            }
        };
    }
}

// Expose as MCP tool:
new McpTool
{
    Name = "validate_python_docs",
    Description = "Validate reStructuredText documentation with doc8 and rst-lint. Returns documentation quality score and formatting issues.",
    InputSchema = new
    {
        type = "object",
        properties = new
        {
            docs_path = new { type = "string", description = "Path to docs/ directory" },
            context = new { type = "string", description = "Project context" }
        },
        required = new[] { "docs_path" }
    }
}
```

---

## 🎓 **Best Practice: Layered Scanning**

### **Layer 1: Fast (Auto-run on save)**
- Ruff (< 1s for most files)
- Basic security (Bandit quick scan)

### **Layer 2: Medium (On file index)**
- Full Ruff analysis
- Full Bandit scan
- Pattern detection

### **Layer 3: Deep (On-demand/pre-commit)**
- Mypy type checking
- Radon complexity analysis
- Dependency vulnerability scan
- Documentation validation

This gives you **fast feedback** during development + **comprehensive analysis** when needed!


