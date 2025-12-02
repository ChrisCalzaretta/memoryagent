# Python Scanner Integration - Working Example

## 🎯 **Real-World Integration: Multi-Tool Python Scanner**

This shows how to add **Ruff + Bandit + Mypy + Radon** as an integrated scanning suite.

---

## 📋 **Step-by-Step Implementation**

### **Step 1: Update Dockerfile**

```dockerfile
# MemoryAgent.Server/Dockerfile

# Add Python scanning tools
RUN apt-get update && apt-get install -y \
    python3 \
    python3-pip \
    python3-venv \
    nodejs \
    npm \
    && pip3 install --break-system-packages --no-cache-dir \
        semgrep \
        ruff \
        bandit \
        mypy \
        radon \
        safety \
        vulture \
        doc8 \
        restructuredtext-lint \
    && npm install -g typescript @types/node \
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/*

# Verify installations
RUN semgrep --version && \
    python3 --version && \
    ruff --version && \
    bandit --version && \
    mypy --version && \
    radon --version
```

---

### **Step 2: Create Unified Python Scanner Service**

```csharp
// MemoryAgent.Server/Services/PythonCodeQualityService.cs

using System.Diagnostics;
using System.Text.Json;

namespace MemoryAgent.Server.Services;

public interface IPythonCodeQualityService
{
    Task<PythonQualityReport> AnalyzeFileAsync(string filePath, CancellationToken ct = default);
    Task<PythonQualityReport> AnalyzeProjectAsync(string projectPath, CancellationToken ct = default);
}

public class PythonCodeQualityService : IPythonCodeQualityService
{
    private readonly ILogger<PythonCodeQualityService> _logger;

    public PythonCodeQualityService(ILogger<PythonCodeQualityService> logger)
    {
        _logger = logger;
    }

    public async Task<PythonQualityReport> AnalyzeFileAsync(string filePath, CancellationToken ct)
    {
        var report = new PythonQualityReport { FilePath = filePath };
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Run all scanners in parallel
            var ruffTask = RunRuffAsync(filePath, ct);
            var banditTask = RunBanditAsync(filePath, ct);
            var mypyTask = RunMypyAsync(filePath, ct);
            var radonTask = RunRadonAsync(filePath, ct);

            await Task.WhenAll(ruffTask, banditTask, mypyTask, radonTask);

            report.RuffResults = ruffTask.Result;
            report.BanditResults = banditTask.Result;
            report.MypyResults = mypyTask.Result;
            report.RadonResults = radonTask.Result;

            // Calculate scores
            report.CodeQualityScore = CalculateCodeQualityScore(report);
            report.SecurityScore = CalculateSecurityScore(report);
            report.TypeCoverageScore = CalculateTypeCoverageScore(report);
            report.ComplexityScore = CalculateComplexityScore(report);
            report.OverallScore = CalculateOverallScore(report);

            // Generate prioritized recommendations
            report.Recommendations = GenerateRecommendations(report);

            stopwatch.Stop();
            report.Duration = stopwatch.Elapsed;
            report.Success = true;

            _logger.LogInformation(
                "Python quality analysis complete: Overall Score: {Score}/10, " +
                "{Recs} recommendations in {Ms}ms",
                report.OverallScore, report.Recommendations.Count, stopwatch.ElapsedMilliseconds);

            return report;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Python quality analysis failed for {File}", filePath);
            report.Errors.Add($"Analysis failed: {ex.Message}");
            report.Duration = stopwatch.Elapsed;
            return report;
        }
    }

    private async Task<RuffScanResult> RunRuffAsync(string filePath, CancellationToken ct)
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "ruff",
                    Arguments = $"check --output-format=json {filePath}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct);

            if (string.IsNullOrWhiteSpace(output))
            {
                return new RuffScanResult { IssueCount = 0 };
            }

            var results = JsonSerializer.Deserialize<List<RuffIssue>>(output);
            
            return new RuffScanResult
            {
                Issues = results ?? new(),
                IssueCount = results?.Count ?? 0,
                ErrorCount = results?.Count(i => i.code?.StartsWith("E") == true) ?? 0,
                WarningCount = results?.Count(i => i.code?.StartsWith("W") == true) ?? 0,
                AutoFixableCount = results?.Count(i => i.fix != null) ?? 0
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ruff scan failed");
            return new RuffScanResult { IssueCount = 0 };
        }
    }

    private async Task<BanditScanResult> RunBanditAsync(string filePath, CancellationToken ct)
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "bandit",
                    Arguments = $"-f json {filePath}",
                    RedirectStandardOutput = true,
                    UseShellExecute = false
                }
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct);

            var results = JsonSerializer.Deserialize<BanditResults>(output);
            
            return new BanditScanResult
            {
                Vulnerabilities = results?.results ?? new(),
                HighSeverity = results?.results.Count(r => r.issue_severity == "HIGH") ?? 0,
                MediumSeverity = results?.results.Count(r => r.issue_severity == "MEDIUM") ?? 0,
                LowSeverity = results?.results.Count(r => r.issue_severity == "LOW") ?? 0
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Bandit scan failed");
            return new BanditScanResult();
        }
    }

    private async Task<MypyScanResult> RunMypyAsync(string filePath, CancellationToken ct)
    {
        try
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
            var output = await process.StandardOutput.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct);

            var errors = ParseMypyOutput(output);
            
            return new MypyScanResult
            {
                TypeErrors = errors,
                TypeErrorCount = errors.Count
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Mypy scan failed");
            return new MypyScanResult();
        }
    }

    private async Task<RadonScanResult> RunRadonAsync(string filePath, CancellationToken ct)
    {
        try
        {
            var ccProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "radon",
                    Arguments = $"cc {filePath} -j",
                    RedirectStandardOutput = true,
                    UseShellExecute = false
                }
            };

            ccProcess.Start();
            var ccOutput = await ccProcess.StandardOutput.ReadToEndAsync(ct);
            await ccProcess.WaitForExitAsync(ct);

            var ccResults = JsonSerializer.Deserialize<Dictionary<string, List<ComplexityItem>>>(ccOutput);
            
            var allMethods = ccResults?.Values.SelectMany(x => x).ToList() ?? new();
            
            return new RadonScanResult
            {
                AverageComplexity = allMethods.Any() ? allMethods.Average(m => m.complexity) : 0,
                MaxComplexity = allMethods.Any() ? allMethods.Max(m => m.complexity) : 0,
                ComplexMethods = allMethods.Where(m => m.complexity > 10).ToList()
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Radon scan failed");
            return new RadonScanResult();
        }
    }

    // SCORING ALGORITHMS
    private double CalculateCodeQualityScore(PythonQualityReport report)
    {
        if (report.RuffResults == null) return 10.0;

        var errorPenalty = report.RuffResults.ErrorCount * 1.5;
        var warningPenalty = report.RuffResults.WarningCount * 0.5;
        
        return Math.Max(0, Math.Min(10, 10.0 - (errorPenalty + warningPenalty)));
    }

    private double CalculateSecurityScore(PythonQualityReport report)
    {
        if (report.BanditResults == null) return 10.0;

        var highPenalty = report.BanditResults.HighSeverity * 3.0;
        var mediumPenalty = report.BanditResults.MediumSeverity * 1.5;
        var lowPenalty = report.BanditResults.LowSeverity * 0.5;
        
        return Math.Max(0, Math.Min(10, 10.0 - (highPenalty + mediumPenalty + lowPenalty)));
    }

    private double CalculateComplexityScore(PythonQualityReport report)
    {
        if (report.RadonResults == null || report.RadonResults.AverageComplexity == 0) 
            return 10.0;

        // Score based on average complexity
        // 1-5: Excellent (10), 6-10: Good (8-9), 11-15: Fair (6-7), 16+: Poor (0-5)
        var avgCC = report.RadonResults.AverageComplexity;
        
        if (avgCC <= 5) return 10.0;
        if (avgCC <= 10) return 9.0 - (avgCC - 5) * 0.2;
        if (avgCC <= 15) return 7.0 - (avgCC - 10) * 0.2;
        return Math.Max(0, 5.0 - (avgCC - 15) * 0.5);
    }

    // RECOMMENDATION GENERATION
    private List<PythonRecommendation> GenerateRecommendations(PythonQualityReport report)
    {
        var recommendations = new List<PythonRecommendation>();

        // Security recommendations
        if (report.BanditResults?.HighSeverity > 0)
        {
            recommendations.Add(new PythonRecommendation
            {
                Priority = "Critical",
                Category = "Security",
                Title = "Fix High-Severity Security Vulnerabilities",
                Description = $"Found {report.BanditResults.HighSeverity} critical security issues",
                Impact = "Prevents potential security breaches",
                AffectedFiles = report.BanditResults.Vulnerabilities
                    .Where(v => v.issue_severity == "HIGH")
                    .Select(v => v.filename)
                    .Distinct()
                    .ToList(),
                Example = report.BanditResults.Vulnerabilities
                    .FirstOrDefault(v => v.issue_severity == "HIGH")?.issue_text
            });
        }

        // Code quality recommendations
        if (report.RuffResults?.ErrorCount > 5)
        {
            recommendations.Add(new PythonRecommendation
            {
                Priority = "High",
                Category = "Code Quality",
                Title = "Fix Linting Errors",
                Description = $"Found {report.RuffResults.ErrorCount} linting errors",
                Impact = $"{report.RuffResults.AutoFixableCount} can be auto-fixed with 'ruff --fix'",
                AutoFixCommand = $"ruff check --fix {report.FilePath}"
            });
        }

        // Type checking recommendations
        if (report.MypyResults?.TypeErrorCount > 0)
        {
            recommendations.Add(new PythonRecommendation
            {
                Priority = "Medium",
                Category = "Type Safety",
                Title = "Add Type Hints",
                Description = $"Found {report.MypyResults.TypeErrorCount} type errors",
                Impact = "Improves code safety and IDE autocomplete",
                Example = "Add type hints to function signatures and class attributes"
            });
        }

        // Complexity recommendations
        if (report.RadonResults?.ComplexMethods?.Count > 0)
        {
            var topComplex = report.RadonResults.ComplexMethods
                .OrderByDescending(m => m.complexity)
                .Take(3)
                .ToList();

            recommendations.Add(new PythonRecommendation
            {
                Priority = "Medium",
                Category = "Maintainability",
                Title = "Refactor Complex Methods",
                Description = $"Found {report.RadonResults.ComplexMethods.Count} methods with complexity > 10",
                Impact = "Improves readability and testability",
                AffectedMethods = topComplex.Select(m => 
                    $"{m.name} (CC: {m.complexity})").ToList(),
                Example = $"Break down {topComplex.First().name} into smaller functions"
            });
        }

        return recommendations.OrderByDescending(r => 
            r.Priority == "Critical" ? 4 : 
            r.Priority == "High" ? 3 : 
            r.Priority == "Medium" ? 2 : 1)
        .ToList();
    }

    private double CalculateOverallScore(PythonQualityReport report)
    {
        // Weighted average: Quality 30%, Security 40%, Complexity 20%, Type Safety 10%
        return (report.CodeQualityScore * 0.3) +
               (report.SecurityScore * 0.4) +
               (report.ComplexityScore * 0.2) +
               (report.TypeCoverageScore * 0.1);
    }
}

// MODELS
public class PythonQualityReport
{
    public string FilePath { get; set; } = "";
    public bool Success { get; set; }
    public TimeSpan Duration { get; set; }
    public List<string> Errors { get; set; } = new();

    // Scanner results
    public RuffScanResult? RuffResults { get; set; }
    public BanditScanResult? BanditResults { get; set; }
    public MypyScanResult? MypyResults { get; set; }
    public RadonScanResult? RadonResults { get; set; }

    // Scores (0-10)
    public double CodeQualityScore { get; set; }
    public double SecurityScore { get; set; }
    public double TypeCoverageScore { get; set; }
    public double ComplexityScore { get; set; }
    public double OverallScore { get; set; }

    // Actionable recommendations
    public List<PythonRecommendation> Recommendations { get; set; } = new();
}

public class PythonRecommendation
{
    public string Priority { get; set; } = ""; // Critical, High, Medium, Low
    public string Category { get; set; } = ""; // Security, Quality, Type Safety, etc.
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Impact { get; set; } = "";
    public string? Example { get; set; }
    public string? AutoFixCommand { get; set; }
    public List<string> AffectedFiles { get; set; } = new();
    public List<string> AffectedMethods { get; set; } = new();
}
```

---

### **Step 3: Add MCP Tool**

```csharp
// MemoryAgent.Server/Services/McpService.cs

private readonly IPythonCodeQualityService _pythonQuality;

public McpService(
    // ... existing services ...
    IPythonCodeQualityService pythonQuality,
    // ...
)
{
    _pythonQuality = pythonQuality;
}

// Add to GetToolsAsync():
new McpTool
{
    Name = "analyze_python_quality",
    Description = "Comprehensive Python code quality analysis with Ruff, Bandit, Mypy, and Radon. Returns quality scores and prioritized recommendations.",
    InputSchema = new
    {
        type = "object",
        properties = new
        {
            path = new { 
                type = "string", 
                description = "Path to Python file or directory to analyze" 
            },
            context = new { 
                type = "string", 
                description = "Optional project context" 
            },
            include_recommendations = new {
                type = "boolean",
                description = "Include actionable recommendations",
                @default = true
            }
        },
        required = new[] { "path" }
    }
}

// Add to CallToolAsync():
"analyze_python_quality" => await AnalyzePythonQualityToolAsync(toolCall.Arguments, cancellationToken)

// Handler:
private async Task<McpToolResult> AnalyzePythonQualityToolAsync(
    JsonElement arguments, 
    CancellationToken ct)
{
    var path = arguments.GetProperty("path").GetString() 
        ?? throw new ArgumentException("path required");
    var context = arguments.TryGetProperty("context", out var ctx) ? ctx.GetString() : null;

    var report = await _pythonQuality.AnalyzeFileAsync(path, ct);

    var text = new StringBuilder();
    text.AppendLine($"# Python Quality Analysis: {Path.GetFileName(path)}");
    text.AppendLine();
    text.AppendLine("## 📊 Scores");
    text.AppendLine($"- **Overall:** {report.OverallScore:F1}/10");
    text.AppendLine($"- Code Quality: {report.CodeQualityScore:F1}/10");
    text.AppendLine($"- Security: {report.SecurityScore:F1}/10");
    text.AppendLine($"- Complexity: {report.ComplexityScore:F1}/10");
    text.AppendLine($"- Type Safety: {report.TypeCoverageScore:F1}/10");
    text.AppendLine();

    text.AppendLine("## 🔍 Tool Results");
    text.AppendLine($"- **Ruff:** {report.RuffResults?.IssueCount ?? 0} issues " +
        $"({report.RuffResults?.AutoFixableCount ?? 0} auto-fixable)");
    text.AppendLine($"- **Bandit:** {report.BanditResults?.Vulnerabilities.Count ?? 0} security issues");
    text.AppendLine($"- **Mypy:** {report.MypyResults?.TypeErrorCount ?? 0} type errors");
    text.AppendLine($"- **Radon:** Avg complexity {report.RadonResults?.AverageComplexity:F1}, " +
        $"{report.RadonResults?.ComplexMethods.Count ?? 0} complex methods");
    text.AppendLine();

    if (report.Recommendations.Any())
    {
        text.AppendLine("## 💡 Top Recommendations");
        text.AppendLine();

        foreach (var rec in report.Recommendations.Take(10))
        {
            var icon = rec.Priority switch
            {
                "Critical" => "🔴",
                "High" => "🟠",
                "Medium" => "🟡",
                _ => "🔵"
            };

            text.AppendLine($"{icon} **{rec.Title}** ({rec.Priority})");
            text.AppendLine($"   *Category:* {rec.Category}");
            text.AppendLine($"   *Impact:* {rec.Impact}");
            
            if (!string.IsNullOrEmpty(rec.AutoFixCommand))
            {
                text.AppendLine($"   *Auto-fix:* `{rec.AutoFixCommand}`");
            }
            
            if (!string.IsNullOrEmpty(rec.Example))
            {
                text.AppendLine($"   *Example:* {rec.Example}");
            }
            
            text.AppendLine();
        }
    }

    return new McpToolResult
    {
        Content = new List<McpContent>
        {
            new McpContent { Type = "text", Text = text.ToString() }
        }
    };
}
```

---

### **Step 4: Register Service**

```csharp
// Program.cs
builder.Services.AddSingleton<IPythonCodeQualityService, PythonCodeQualityService>();
```

---

## 🎯 **Expected Output**

When you call `analyze_python_quality`:

```
# Python Quality Analysis: myfile.py

## 📊 Scores
- **Overall:** 7.8/10
- Code Quality: 8.5/10
- Security: 9.0/10
- Complexity: 6.5/10
- Type Safety: 7.0/10

## 🔍 Tool Results
- **Ruff:** 12 issues (8 auto-fixable)
- **Bandit:** 2 security issues
- **Mypy:** 5 type errors
- **Radon:** Avg complexity 8.2, 3 complex methods

## 💡 Top Recommendations

🟠 **Fix Linting Errors** (High)
   *Category:* Code Quality
   *Impact:* 8 can be auto-fixed with 'ruff --fix'
   *Auto-fix:* `ruff check --fix /workspace/myfile.py`

🟡 **Refactor Complex Methods** (Medium)
   *Category:* Maintainability
   *Impact:* Improves readability and testability
   *Example:* Break down process_data (CC: 15) into smaller functions

🟡 **Add Type Hints** (Medium)
   *Category:* Type Safety
   *Impact:* Improves code safety and IDE autocomplete
   *Example:* Add type hints to function signatures and class attributes
```

---

## 🚀 **Usage**

```bash
# From Cursor AI or CLI
curl -X POST http://localhost:5000/mcp/call \
  -d '{
    "method": "tools/call",
    "params": {
      "name": "analyze_python_quality",
      "arguments": {
        "path": "/workspace/AgentTrader/main.py"
      }
    }
  }'
```

---

This gives you **unified Python quality reporting** with actionable recommendations!

