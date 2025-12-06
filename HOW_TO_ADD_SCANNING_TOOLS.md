# How to Add Custom Scanning Tools with Recommendations

## 🏗️ **Architecture Overview**

The Memory Agent uses a modular architecture for scanners:

```
User Request
    ↓
MCP Tool (exposed to AI/user)
    ↓
Scanner Service (business logic)
    ↓
External Tool/Analysis (Semgrep, custom analyzers, etc.)
    ↓
Results → Recommendations
    ↓
Storage (Neo4j/Qdrant)
```

---

## 📚 **3 Ways to Add Scanning Tools**

### **Option 1: Add as MCP Tool (Recommended)**
- ✅ Exposed to AI agents and Cursor IDE
- ✅ Discoverable through MCP protocol
- ✅ Auto-documented with schema

### **Option 2: Add to Indexing Pipeline**
- ✅ Runs automatically during file indexing
- ✅ Integrated with existing workflow
- ❌ Not directly callable

### **Option 3: Standalone Service + Both**
- ✅ Best of both worlds
- ✅ Auto-runs during indexing + manually callable
- ✅ Most flexible

---

## 🛠️ **Step-by-Step: Adding a Custom Scanner**

### **Example: Adding ESLint Scanner for JavaScript**

## Step 1: Create the Scanner Service Interface

```csharp
// MemoryAgent.Server/Services/IEslintService.cs
namespace MemoryAgent.Server.Services;

public interface IEslintService
{
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);
    Task<EslintReport> ScanFileAsync(string filePath, CancellationToken cancellationToken = default);
    Task<EslintReport> ScanDirectoryAsync(string directoryPath, CancellationToken cancellationToken = default);
}

// Models for results
public class EslintReport
{
    public bool Success { get; set; }
    public string FilePath { get; set; } = "";
    public List<EslintFinding> Findings { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    public TimeSpan Duration { get; set; }
    
    // Recommendations
    public List<EslintRecommendation> Recommendations { get; set; } = new();
    public double QualityScore { get; set; } // 0-10
}

public class EslintFinding
{
    public string RuleId { get; set; } = "";
    public string Message { get; set; } = "";
    public string Severity { get; set; } = ""; // error, warning, info
    public int Line { get; set; }
    public int Column { get; set; }
    public string CodeSnippet { get; set; } = "";
    public string? Fix { get; set; }
}

public class EslintRecommendation
{
    public string Category { get; set; } = ""; // "Code Quality", "Performance", "Security"
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Priority { get; set; } = ""; // "High", "Medium", "Low"
    public string? Example { get; set; }
    public List<string> AffectedFiles { get; set; } = new();
}
```

---

## Step 2: Implement the Scanner Service

```csharp
// MemoryAgent.Server/Services/EslintService.cs
using System.Diagnostics;
using System.Text.Json;

namespace MemoryAgent.Server.Services;

public class EslintService : IEslintService
{
    private readonly ILogger<EslintService> _logger;
    private bool? _isAvailable;

    public EslintService(ILogger<EslintService> logger)
    {
        _logger = logger;
    }

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        if (_isAvailable.HasValue)
            return _isAvailable.Value;

        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "npx",
                    Arguments = "eslint --version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            await process.WaitForExitAsync(cancellationToken);

            _isAvailable = process.ExitCode == 0;
            return _isAvailable.Value;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ESLint not available");
            _isAvailable = false;
            return false;
        }
    }

    public async Task<EslintReport> ScanFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var report = new EslintReport { FilePath = filePath };
        var stopwatch = Stopwatch.StartNew();

        try
        {
            if (!await IsAvailableAsync(cancellationToken))
            {
                report.Errors.Add("ESLint not available");
                return report;
            }

            _logger.LogInformation("Running ESLint scan on: {File}", filePath);

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "npx",
                    Arguments = $"eslint --format json {filePath}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            var error = await process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            stopwatch.Stop();
            report.Duration = stopwatch.Elapsed;

            if (!string.IsNullOrWhiteSpace(output))
            {
                // Parse ESLint JSON output
                var results = JsonSerializer.Deserialize<List<EslintFileResult>>(output);
                if (results?.Any() == true)
                {
                    foreach (var finding in results[0].messages ?? new())
                    {
                        report.Findings.Add(new EslintFinding
                        {
                            RuleId = finding.ruleId ?? "unknown",
                            Message = finding.message ?? "",
                            Severity = finding.severity == 2 ? "error" : "warning",
                            Line = finding.line,
                            Column = finding.column,
                            CodeSnippet = ExtractCodeSnippet(filePath, finding.line),
                            Fix = finding.fix?.text
                        });
                    }
                }
            }

            // Generate recommendations
            report.Recommendations = GenerateRecommendations(report.Findings);
            report.QualityScore = CalculateQualityScore(report.Findings);
            report.Success = true;

            _logger.LogInformation("ESLint scan complete: {Count} findings, Quality Score: {Score}/10 in {Duration}ms",
                report.Findings.Count, report.QualityScore, report.Duration.TotalMilliseconds);

            return report;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ESLint scan failed for {File}", filePath);
            report.Errors.Add($"Scan failed: {ex.Message}");
            report.Duration = stopwatch.Elapsed;
            return report;
        }
    }

    private List<EslintRecommendation> GenerateRecommendations(List<EslintFinding> findings)
    {
        var recommendations = new List<EslintRecommendation>();

        // Group findings by category
        var errorCount = findings.Count(f => f.Severity == "error");
        var warningCount = findings.Count(f => f.Severity == "warning");

        if (errorCount > 0)
        {
            recommendations.Add(new EslintRecommendation
            {
                Category = "Code Quality",
                Title = "Fix Critical ESLint Errors",
                Description = $"Found {errorCount} critical errors that must be fixed.",
                Priority = "High",
                Example = findings.FirstOrDefault(f => f.Severity == "error")?.Message,
                AffectedFiles = findings.Select(f => f.RuleId).Distinct().ToList()
            });
        }

        // Check for specific rule violations
        var noUnusedVars = findings.Where(f => f.RuleId == "no-unused-vars").ToList();
        if (noUnusedVars.Any())
        {
            recommendations.Add(new EslintRecommendation
            {
                Category = "Code Cleanup",
                Title = "Remove Unused Variables",
                Description = $"Found {noUnusedVars.Count} unused variables. Remove them to improve code clarity.",
                Priority = "Medium",
                Example = noUnusedVars.First().Message
            });
        }

        return recommendations;
    }

    private double CalculateQualityScore(List<EslintFinding> findings)
    {
        if (!findings.Any()) return 10.0;

        var errorPenalty = findings.Count(f => f.Severity == "error") * 1.5;
        var warningPenalty = findings.Count(f => f.Severity == "warning") * 0.5;
        
        var score = 10.0 - (errorPenalty + warningPenalty);
        return Math.Max(0, Math.Min(10, score));
    }

    private string ExtractCodeSnippet(string filePath, int lineNumber)
    {
        try
        {
            var lines = File.ReadAllLines(filePath);
            if (lineNumber > 0 && lineNumber <= lines.Length)
            {
                return lines[lineNumber - 1].Trim();
            }
        }
        catch { }
        return "";
    }

    // Helper class for JSON parsing
    private class EslintFileResult
    {
        public List<EslintMessage>? messages { get; set; }
    }

    private class EslintMessage
    {
        public string? ruleId { get; set; }
        public string? message { get; set; }
        public int severity { get; set; }
        public int line { get; set; }
        public int column { get; set; }
        public EslintFix? fix { get; set; }
    }

    private class EslintFix
    {
        public string? text { get; set; }
    }
}
```

---

## Step 3: Register the Service (Dependency Injection)

```csharp
// MemoryAgent.Server/Program.cs

// Add this with other service registrations
builder.Services.AddSingleton<IEslintService, EslintService>();
```

---

## Step 4: Expose as MCP Tool

```csharp
// MemoryAgent.Server/Services/McpService.cs

// 1. Inject the service in constructor
private readonly IEslintService _eslintService;

public McpService(
    // ... existing parameters ...
    IEslintService eslintService,
    // ... rest ...
)
{
    // ... existing assignments ...
    _eslintService = eslintService;
}

// 2. Add tool definition in GetToolsAsync()
public Task<List<McpTool>> GetToolsAsync(CancellationToken cancellationToken = default)
{
    var tools = new List<McpTool>
    {
        // ... existing tools ...
        
        new McpTool
        {
            Name = "scan_with_eslint",
            Description = "Scan JavaScript/TypeScript files with ESLint and get code quality recommendations",
            InputSchema = new
            {
                type = "object",
                properties = new
                {
                    path = new { 
                        type = "string", 
                        description = "Path to JavaScript/TypeScript file or directory" 
                    },
                    context = new { 
                        type = "string", 
                        description = "Optional project context" 
                    }
                },
                required = new[] { "path" }
            }
        }
    };

    return Task.FromResult(tools);
}

// 3. Add tool handler in CallToolAsync()
public async Task<McpToolResult> CallToolAsync(McpToolCall toolCall, CancellationToken cancellationToken = default)
{
    return toolCall.Name switch
    {
        // ... existing cases ...
        "scan_with_eslint" => await ScanWithEslintToolAsync(toolCall.Arguments, cancellationToken),
        _ => throw new ArgumentException($"Unknown tool: {toolCall.Name}")
    };
}

// 4. Implement the tool handler
private async Task<McpToolResult> ScanWithEslintToolAsync(
    JsonElement arguments, 
    CancellationToken cancellationToken)
{
    var path = arguments.GetProperty("path").GetString() ?? throw new ArgumentException("path required");
    var context = arguments.TryGetProperty("context", out var ctx) ? ctx.GetString() : null;

    // Run the scan
    var report = File.GetAttributes(path).HasFlag(FileAttributes.Directory)
        ? await _eslintService.ScanDirectoryAsync(path, cancellationToken)
        : await _eslintService.ScanFileAsync(path, cancellationToken);

    // Format results for display
    var result = new McpToolResult
    {
        Content = new List<McpContent>
        {
            new McpContent
            {
                Type = "text",
                Text = FormatEslintReport(report)
            }
        }
    };

    return result;
}

private string FormatEslintReport(EslintReport report)
{
    var sb = new StringBuilder();
    
    sb.AppendLine($"# ESLint Scan Results: {Path.GetFileName(report.FilePath)}");
    sb.AppendLine();
    sb.AppendLine($"**Quality Score:** {report.QualityScore:F1}/10");
    sb.AppendLine($"**Findings:** {report.Findings.Count}");
    sb.AppendLine($"**Duration:** {report.Duration.TotalMilliseconds:F0}ms");
    sb.AppendLine();

    // Findings
    if (report.Findings.Any())
    {
        sb.AppendLine("## 🔍 Findings");
        sb.AppendLine();

        foreach (var finding in report.Findings.Take(10))
        {
            var icon = finding.Severity == "error" ? "❌" : "⚠️";
            sb.AppendLine($"{icon} **Line {finding.Line}**: {finding.Message}");
            sb.AppendLine($"   Rule: `{finding.RuleId}`, Severity: {finding.Severity}");
            if (!string.IsNullOrEmpty(finding.CodeSnippet))
            {
                sb.AppendLine($"   Code: `{finding.CodeSnippet}`");
            }
            sb.AppendLine();
        }

        if (report.Findings.Count > 10)
        {
            sb.AppendLine($"_... and {report.Findings.Count - 10} more findings_");
            sb.AppendLine();
        }
    }

    // Recommendations
    if (report.Recommendations.Any())
    {
        sb.AppendLine("## 💡 Recommendations");
        sb.AppendLine();

        foreach (var rec in report.Recommendations)
        {
            var icon = rec.Priority == "High" ? "🔴" : rec.Priority == "Medium" ? "🟡" : "🔵";
            sb.AppendLine($"{icon} **{rec.Title}** ({rec.Priority} Priority)");
            sb.AppendLine($"   Category: {rec.Category}");
            sb.AppendLine($"   {rec.Description}");
            if (!string.IsNullOrEmpty(rec.Example))
            {
                sb.AppendLine($"   Example: {rec.Example}");
            }
            sb.AppendLine();
        }
    }

    if (!report.Success && report.Errors.Any())
    {
        sb.AppendLine("## ❌ Errors");
        foreach (var error in report.Errors)
        {
            sb.AppendLine($"- {error}");
        }
    }

    return sb.ToString();
}
```

---

## Step 5: (Optional) Integrate into Indexing Pipeline

```csharp
// MemoryAgent.Server/Services/IndexingService.cs

public async Task<IndexResult> IndexFileAsync(string filePath, string? context, CancellationToken ct)
{
    // ... existing code ...

    // After Semgrep scan, add ESLint scan for JS/TS files
    if (Path.GetExtension(filePath).ToLower() is ".js" or ".jsx" or ".ts" or ".tsx")
    {
        try
        {
            var eslintReport = await _eslintService.ScanFileAsync(containerPath, ct);
            
            if (eslintReport.Success && eslintReport.Findings.Any())
            {
                _logger.LogInformation("ESLint found {Count} issues in {File}", 
                    eslintReport.Findings.Count, containerPath);

                // Store findings as patterns or in metadata
                foreach (var finding in eslintReport.Findings)
                {
                    var eslintPattern = new CodePattern
                    {
                        Type = PatternType.CodeQuality,
                        Category = PatternCategory.BestPractice,
                        Name = $"ESLint_{finding.RuleId}_{Path.GetFileName(containerPath)}",
                        Implementation = finding.Message,
                        FilePath = containerPath,
                        LineNumber = finding.Line,
                        Content = finding.CodeSnippet,
                        IsPositivePattern = false,
                        Confidence = finding.Severity == "error" ? 1.0f : 0.7f,
                        Context = context ?? "default",
                        Metadata = new Dictionary<string, object>
                        {
                            ["eslint_rule"] = finding.RuleId,
                            ["severity"] = finding.Severity,
                            ["fix"] = finding.Fix ?? ""
                        }
                    };

                    // Store in Neo4j
                    await _graphService.StorePatternNodeAsync(eslintPattern, ct);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ESLint scan failed for {File}", containerPath);
        }
    }

    // ... rest of indexing ...
}
```

---

## 🎯 **Quick Reference: Scanner Integration Patterns**

### **Pattern 1: External CLI Tool** (Semgrep, ESLint, pylint)
```csharp
var process = new Process
{
    StartInfo = new ProcessStartInfo
    {
        FileName = "tool-name",
        Arguments = "--json output.json",
        RedirectStandardOutput = true,
        UseShellExecute = false
    }
};
```

### **Pattern 2: .NET Library** (Roslyn analyzers, NuGet packages)
```csharp
// Use the library directly
var analyzer = new CodeAnalyzer();
var results = await analyzer.AnalyzeAsync(code);
```

### **Pattern 3: HTTP API** (SonarQube, CodeClimate, etc.)
```csharp
var client = new HttpClient();
var response = await client.PostAsync("https://api.service.com/analyze", content);
var results = await response.Content.ReadFromJsonAsync<ScanResults>();
```

---

## 📦 **Ready-to-Use Scanner Examples**

### 1. **Pylint** (Python)
```csharp
Arguments = $"pylint --output-format=json {filePath}"
```

### 2. **Prettier** (Code Formatting)
```csharp
Arguments = $"prettier --check --list-different {filePath}"
```

### 3. **SonarScanner** (Comprehensive Analysis)
```csharp
Arguments = $"sonar-scanner -Dsonar.sources={directory}"
```

### 4. **Trivy** (Security/Vulnerabilities)
```csharp
Arguments = $"trivy fs --format json {directory}"
```

### 5. **CodeQL** (Security Analysis)
```csharp
Arguments = $"codeql database analyze --format=sarif-latest"
```

---

## 🚀 **Testing Your Scanner**

```bash
# 1. Test the MCP tool from command line
curl -X POST http://localhost:5000/mcp/call \
  -H "Content-Type: application/json" \
  -d '{
    "method": "tools/call",
    "params": {
      "name": "scan_with_eslint",
      "arguments": {
        "path": "/workspace/project/src/index.js"
      }
    }
  }'

# 2. Verify tool is listed
curl http://localhost:5000/mcp/tools
```

---

## ✨ **Best Practices**

1. ✅ **Always check if tool is available** before scanning
2. ✅ **Use async/await** for all I/O operations
3. ✅ **Add timeouts** to prevent hanging
4. ✅ **Log detailed info** for debugging
5. ✅ **Return structured data** (JSON) for recommendations
6. ✅ **Cache availability checks** (don't run `--version` every time)
7. ✅ **Handle errors gracefully** (tool not installed, file not found, etc.)
8. ✅ **Provide actionable recommendations** (not just problems)
9. ✅ **Include code examples** in recommendations
10. ✅ **Add quality/severity scores** for prioritization

---

## 🎓 **Advanced: Multi-Tool Aggregator**

You can create a meta-scanner that runs multiple tools and aggregates results:

```csharp
public class CodeQualityScannerService
{
    private readonly IEslintService _eslint;
    private readonly ISemgrepService _semgrep;
    private readonly IPylintService _pylint;

    public async Task<AggregatedReport> ScanAsync(string path)
    {
        var tasks = new List<Task>
        {
            _eslint.ScanFileAsync(path),
            _semgrep.ScanFileAsync(path),
            _pylint.ScanFileAsync(path)
        };

        await Task.WhenAll(tasks);

        // Aggregate results, deduplicate, prioritize recommendations
        return AggregateResults(tasks);
    }
}
```

This gives you a **unified code quality report** from multiple sources!






