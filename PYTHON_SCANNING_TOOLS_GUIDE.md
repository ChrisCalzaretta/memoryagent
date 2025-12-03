# Python Scanning Tools - Complete Guide

## 🐍 **Python-Specific Scanners You Can Add**

### **1. PYLINT** - Comprehensive Code Quality

**What it does:**
- Code quality scoring (0-10)
- PEP 8 style violations
- Code smells and bad practices
- Unused imports/variables
- Complex code detection

**Installation:**
```bash
pip install pylint
```

**CLI Usage:**
```bash
pylint --output-format=json myfile.py
```

**Integration Example:**
```csharp
public class PylintService : IPylintService
{
    public async Task<PylintReport> ScanFileAsync(string filePath)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "pylint",
                Arguments = $"--output-format=json {filePath}",
                RedirectStandardOutput = true,
                UseShellExecute = false
            }
        };

        process.Start();
        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();

        // Parse JSON output
        var results = JsonSerializer.Deserialize<PylintResult[]>(output);
        
        return GenerateReport(results);
    }
}
```

**JSON Output Example:**
```json
[
  {
    "type": "convention",
    "module": "myfile",
    "obj": "MyClass.my_method",
    "line": 42,
    "column": 4,
    "message": "Missing function docstring",
    "message-id": "C0116",
    "symbol": "missing-function-docstring"
  }
]
```

**Recommendations Generated:**
- Code quality score: 7.5/10
- Fix 5 high-priority issues
- Add docstrings to 12 functions
- Simplify 3 complex methods

---

### **2. BANDIT** - Security Scanner

**What it does:**
- Security vulnerability detection
- Hardcoded passwords/secrets
- SQL injection risks
- Unsafe deserialization
- Weak cryptography

**Installation:**
```bash
pip install bandit
```

**CLI Usage:**
```bash
bandit -r /path/to/code -f json -o results.json
```

**Integration Example:**
```csharp
public class BanditService : IBanditService
{
    public async Task<BanditReport> ScanDirectoryAsync(string directory)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "bandit",
                Arguments = $"-r {directory} -f json",
                RedirectStandardOutput = true,
                UseShellExecute = false
            }
        };

        process.Start();
        var output = await process.StandardOutput.ReadToEndAsync();
        
        var results = JsonSerializer.Deserialize<BanditResults>(output);
        
        return new BanditReport
        {
            SecurityScore = CalculateSecurityScore(results),
            Vulnerabilities = results.results,
            Recommendations = GenerateSecurityRecommendations(results)
        };
    }

    private List<SecurityRecommendation> GenerateSecurityRecommendations(BanditResults results)
    {
        var recommendations = new List<SecurityRecommendation>();

        // High severity issues
        var highSeverity = results.results.Where(r => r.issue_severity == "HIGH");
        if (highSeverity.Any())
        {
            recommendations.Add(new SecurityRecommendation
            {
                Priority = "Critical",
                Category = "Security",
                Title = "Fix High-Severity Security Issues",
                Description = $"Found {highSeverity.Count()} critical security vulnerabilities",
                AffectedFiles = highSeverity.Select(r => r.filename).Distinct().ToList(),
                Examples = highSeverity.Take(3).Select(r => 
                    $"{r.test_id}: {r.issue_text} (Line {r.line_number})").ToList()
            });
        }

        return recommendations;
    }
}
```

**JSON Output Example:**
```json
{
  "results": [
    {
      "code": "password = 'hardcoded_password'",
      "filename": "app/config.py",
      "issue_confidence": "HIGH",
      "issue_severity": "HIGH",
      "issue_text": "Possible hardcoded password: 'hardcoded_password'",
      "line_number": 15,
      "test_id": "B105",
      "test_name": "hardcoded_password_string"
    }
  ]
}
```

---

### **3. MYPY** - Type Checking

**What it does:**
- Static type checking
- Type hint validation
- Catches type errors before runtime
- Improves code documentation

**Installation:**
```bash
pip install mypy
```

**CLI Usage:**
```bash
mypy --json-report report.json myfile.py
```

**Integration Example:**
```csharp
public class MypyService : IMypyService
{
    public async Task<MypyReport> ScanFileAsync(string filePath)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "mypy",
                Arguments = $"--show-column-numbers --no-error-summary {filePath}",
                RedirectStandardOutput = true,
                UseShellExecute = false
            }
        };

        process.Start();
        var output = await process.StandardOutput.ReadToEndAsync();
        
        // Parse mypy output (line:col: error: message)
        var errors = ParseMypyOutput(output);
        
        return new MypyReport
        {
            TypeSafetyScore = CalculateTypeScore(errors),
            Errors = errors,
            Recommendations = GenerateTypeRecommendations(errors)
        };
    }
}
```

**Recommendations:**
- Add type hints to 15 functions
- Fix 8 type mismatches
- Type coverage: 65% → target 90%

---

### **4. RADON** - Complexity Metrics

**What it does:**
- Cyclomatic complexity (CC)
- Maintainability Index (MI)
- Halstead metrics
- Raw metrics (LOC, LLOC, etc.)

**Installation:**
```bash
pip install radon
```

**CLI Usage:**
```bash
radon cc myfile.py -j        # Cyclomatic complexity
radon mi myfile.py -j        # Maintainability index
radon hal myfile.py -j       # Halstead metrics
```

**Integration Example:**
```csharp
public class RadonService : IRadonService
{
    public async Task<RadonReport> AnalyzeComplexityAsync(string filePath)
    {
        // Run cyclomatic complexity
        var ccOutput = await RunRadonCommand($"cc {filePath} -j");
        var miOutput = await RunRadonCommand($"mi {filePath} -j");
        
        var ccResults = JsonSerializer.Deserialize<Dictionary<string, List<ComplexityItem>>>(ccOutput);
        var miResults = JsonSerializer.Deserialize<Dictionary<string, MaintainabilityItem>>(miOutput);
        
        return new RadonReport
        {
            ComplexityScore = CalculateOverallComplexity(ccResults),
            MaintainabilityIndex = miResults.Values.FirstOrDefault()?.mi ?? 0,
            ComplexMethods = ccResults.Values.SelectMany(x => x)
                .Where(x => x.complexity > 10)
                .ToList(),
            Recommendations = GenerateComplexityRecommendations(ccResults, miResults)
        };
    }

    private List<ComplexityRecommendation> GenerateComplexityRecommendations(
        Dictionary<string, List<ComplexityItem>> ccResults,
        Dictionary<string, MaintainabilityItem> miResults)
    {
        var recommendations = new List<ComplexityRecommendation>();

        // Find highly complex methods
        var complexMethods = ccResults.Values
            .SelectMany(x => x)
            .Where(x => x.complexity > 10)
            .OrderByDescending(x => x.complexity)
            .ToList();

        if (complexMethods.Any())
        {
            recommendations.Add(new ComplexityRecommendation
            {
                Priority = "High",
                Title = "Refactor Complex Methods",
                Description = $"Found {complexMethods.Count} methods with complexity > 10",
                AffectedMethods = complexMethods.Select(m => 
                    $"{m.name} (CC: {m.complexity})").ToList(),
                Suggestion = "Break down into smaller functions, extract helper methods"
            });
        }

        // Low maintainability
        var lowMI = miResults.Where(x => x.Value.mi < 20).ToList();
        if (lowMI.Any())
        {
            recommendations.Add(new ComplexityRecommendation
            {
                Priority = "Medium",
                Title = "Improve Code Maintainability",
                Description = $"{lowMI.Count} files have low maintainability index",
                AffectedFiles = lowMI.Select(x => x.Key).ToList(),
                Suggestion = "Reduce complexity, improve documentation, remove duplication"
            });
        }

        return recommendations;
    }
}
```

**JSON Output Example (CC):**
```json
{
  "myfile.py": [
    {
      "type": "method",
      "name": "MyClass.complex_method",
      "lineno": 42,
      "complexity": 15,
      "rank": "C"
    }
  ]
}
```

**Recommendations:**
- Refactor 3 methods with CC > 10
- Improve maintainability (MI: 58 → 70)
- Reduce file from 500 to 300 LOC

---

### **5. VULTURE** - Dead Code Detection

**What it does:**
- Finds unused code
- Detects unused imports
- Identifies unreachable code
- Reports unused variables/functions

**Installation:**
```bash
pip install vulture
```

**CLI Usage:**
```bash
vulture mycode/ --json
```

**Integration:**
```csharp
public class VultureService : IVultureService
{
    public async Task<VultureReport> ScanAsync(string path)
    {
        var output = await RunCommand($"vulture {path}");
        
        // Parse output for dead code
        var deadCode = ParseVultureOutput(output);
        
        return new VultureReport
        {
            UnusedCode = deadCode,
            Recommendations = new[]
            {
                new Recommendation
                {
                    Title = "Remove Dead Code",
                    Description = $"Found {deadCode.Count} unused code segments",
                    Impact = "Reduces codebase size by ~" + deadCode.Sum(d => d.LineCount) + " lines"
                }
            }
        };
    }
}
```

---

### **6. SAFETY** - Dependency Vulnerabilities

**What it does:**
- Scans Python dependencies
- Checks for known vulnerabilities
- Reports CVE information
- Suggests package updates

**Installation:**
```bash
pip install safety
```

**CLI Usage:**
```bash
safety check --json
```

**Integration:**
```csharp
public class SafetyService : ISafetyService
{
    public async Task<SafetyReport> ScanDependenciesAsync(string projectPath)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "safety",
                Arguments = "check --json",
                WorkingDirectory = projectPath,
                RedirectStandardOutput = true,
                UseShellExecute = false
            }
        };

        process.Start();
        var output = await process.StandardOutput.ReadToEndAsync();
        
        var vulnerabilities = JsonSerializer.Deserialize<SafetyVulnerability[]>(output);
        
        return new SafetyReport
        {
            VulnerablePackages = vulnerabilities.Length,
            SecurityRisk = CalculateRisk(vulnerabilities),
            Vulnerabilities = vulnerabilities,
            Recommendations = GenerateDependencyRecommendations(vulnerabilities)
        };
    }

    private List<DependencyRecommendation> GenerateDependencyRecommendations(
        SafetyVulnerability[] vulnerabilities)
    {
        return vulnerabilities
            .GroupBy(v => v.package)
            .Select(g => new DependencyRecommendation
            {
                Priority = g.Any(v => v.severity == "high") ? "Critical" : "High",
                Package = g.Key,
                CurrentVersion = g.First().installed_version,
                SafeVersion = g.First().fixed_in,
                Description = $"Upgrade {g.Key} to fix {g.Count()} vulnerabilities",
                CVEs = g.SelectMany(v => v.cve.Split(',')).Distinct().ToList()
            })
            .ToList();
    }
}
```

---

### **7. RUFF** - Modern Fast Linter

**What it does:**
- Extremely fast (10-100x faster than Flake8)
- Combines Flake8, isort, pydocstyle, and more
- Auto-fixes many issues
- Written in Rust

**Installation:**
```bash
pip install ruff
```

**CLI Usage:**
```bash
ruff check --output-format=json myfile.py
```

**Integration:**
```csharp
public class RuffService : IRuffService
{
    public async Task<RuffReport> ScanAsync(string path)
    {
        var output = await RunCommand($"ruff check --output-format=json {path}");
        var results = JsonSerializer.Deserialize<RuffResult[]>(output);
        
        return new RuffReport
        {
            Issues = results,
            AutoFixable = results.Count(r => r.fix != null),
            Recommendations = GenerateRuffRecommendations(results)
        };
    }
}
```

---

### **8. DOC8 / RST-LINT** - Documentation Validation

**What it does:**
- Validates reStructuredText (.rst) files
- Checks Sphinx documentation
- Ensures proper formatting
- Detects broken references

**Installation:**
```bash
pip install doc8
pip install restructuredtext-lint
```

**CLI Usage:**
```bash
doc8 docs/ --quiet
rst-lint README.rst
```

**Integration:**
```csharp
public class RstLintService : IRstLintService
{
    public async Task<RstLintReport> ScanAsync(string rstFile)
    {
        var doc8Output = await RunCommand($"doc8 {rstFile}");
        var rstLintOutput = await RunCommand($"rst-lint {rstFile}");
        
        return new RstLintReport
        {
            Doc8Issues = ParseDoc8(doc8Output),
            RstLintIssues = ParseRstLint(rstLintOutput),
            Recommendations = new[]
            {
                new Recommendation
                {
                    Title = "Fix Documentation Issues",
                    Description = "Ensure all .rst files are properly formatted",
                    Impact = "Improves documentation quality and Sphinx build success"
                }
            }
        };
    }
}
```

---

## 🎯 **RECOMMENDED SCANNER COMBO FOR PYTHON**

### **Essential Trio:**
1. **Ruff** - Fast linting + auto-fix
2. **Bandit** - Security scanning
3. **Mypy** - Type checking

### **Advanced Stack:**
1. **Ruff** - Code quality
2. **Bandit** - Security
3. **Mypy** - Type safety
4. **Radon** - Complexity metrics
5. **Safety** - Dependency vulnerabilities

---

## 💡 **MULTI-TOOL AGGREGATOR**

Combine multiple Python scanners:

```csharp
public class PythonCodeQualityService
{
    private readonly IRuffService _ruff;
    private readonly IBanditService _bandit;
    private readonly IMypyService _mypy;
    private readonly IRadonService _radon;
    private readonly ISafetyService _safety;

    public async Task<AggregatedPythonReport> AnalyzeAsync(string projectPath)
    {
        // Run all scanners in parallel
        var tasks = new[]
        {
            _ruff.ScanAsync(projectPath),
            _bandit.ScanDirectoryAsync(projectPath),
            _mypy.ScanFileAsync(projectPath),
            _radon.AnalyzeComplexityAsync(projectPath),
            _safety.ScanDependenciesAsync(projectPath)
        };

        await Task.WhenAll(tasks);

        // Aggregate results
        return new AggregatedPythonReport
        {
            OverallScore = CalculateOverallScore(tasks),
            CodeQuality = tasks[0].Result,
            SecurityScore = tasks[1].Result.SecurityScore,
            TypeCoverage = tasks[2].Result.TypeCoverage,
            Complexity = tasks[3].Result.ComplexityScore,
            DependencyRisk = tasks[4].Result.SecurityRisk,
            
            // Prioritized recommendations from all tools
            TopRecommendations = AggregateRecommendations(tasks)
                .OrderByDescending(r => r.Priority)
                .Take(10)
                .ToList()
        };
    }
}
```

---

## 🚀 **QUICK START: Add Bandit Scanner**

**1. Create Service:**
```csharp
// Services/BanditService.cs
public class BanditService : IBanditService { /* ... */ }
```

**2. Register:**
```csharp
// Program.cs
builder.Services.AddSingleton<IBanditService, BanditService>();
```

**3. Add MCP Tool:**
```csharp
// McpService.cs
new McpTool
{
    Name = "scan_python_security",
    Description = "Scan Python code for security vulnerabilities with Bandit"
}
```

**4. Docker:**
```dockerfile
# Dockerfile
RUN pip install bandit
```

---

## 📊 **COMPARISON TABLE**

| Tool | Speed | Coverage | Auto-Fix | JSON Output |
|------|-------|----------|----------|-------------|
| **Ruff** | ⚡⚡⚡ | ⭐⭐⭐ | ✅ | ✅ |
| **Pylint** | ⚡ | ⭐⭐⭐ | ❌ | ✅ |
| **Bandit** | ⚡⚡ | ⭐⭐ (security) | ❌ | ✅ |
| **Mypy** | ⚡⚡ | ⭐⭐ (types) | ❌ | ✅ |
| **Radon** | ⚡⚡⚡ | ⭐⭐ (metrics) | ❌ | ✅ |
| **Safety** | ⚡⚡⚡ | ⭐⭐⭐ (deps) | ❌ | ✅ |

---

## 🎓 **Best Practices**

1. ✅ Run **Ruff** or **Pylint** for general quality
2. ✅ Run **Bandit** for security on all Python code
3. ✅ Run **Mypy** if project uses type hints
4. ✅ Run **Radon** to track complexity over time
5. ✅ Run **Safety** on every dependency update
6. ✅ Aggregate results for unified reports
7. ✅ Cache tool availability checks
8. ✅ Run scanners in parallel for speed
9. ✅ Store historical data to track improvements
10. ✅ Generate actionable recommendations

---

This gives you **comprehensive Python code analysis** with security, quality, type safety, complexity, and dependency tracking!



