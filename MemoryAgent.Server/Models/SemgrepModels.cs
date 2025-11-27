using System.Text.Json;
using System.Text.Json.Serialization;

namespace MemoryAgent.Server.Models;

/// <summary>
/// Semgrep scan report
/// </summary>
public class SemgrepReport
{
    public string FilePath { get; set; } = string.Empty;
    public List<SemgrepFinding> Findings { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    public bool Success { get; set; } = true;
    public DateTime ScannedAt { get; set; } = DateTime.UtcNow;
    public double DurationSeconds { get; set; }
}

/// <summary>
/// Individual Semgrep finding (vulnerability)
/// </summary>
public class SemgrepFinding
{
    public string RuleId { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public int StartLine { get; set; }
    public int EndLine { get; set; }
    public int StartColumn { get; set; }
    public int EndColumn { get; set; }
    public string Severity { get; set; } = "WARNING";
    public string CodeSnippet { get; set; } = string.Empty;
    public SemgrepMetadata Metadata { get; set; } = new();
    public string? Fix { get; set; }
}

/// <summary>
/// Semgrep metadata (CWE, OWASP, etc.)
/// </summary>
public class SemgrepMetadata
{
    public string? CWE { get; set; }
    public string? OWASP { get; set; }
    public string? Category { get; set; }
    public string? Confidence { get; set; }
    public string? Impact { get; set; }
    public string? Likelihood { get; set; }
}

/// <summary>
/// Raw Semgrep JSON output structure
/// </summary>
public class SemgrepOutput
{
    [JsonPropertyName("results")]
    public List<SemgrepResult> Results { get; set; } = new();
    
    [JsonPropertyName("errors")]
    public List<SemgrepError> Errors { get; set; } = new();
    
    [JsonPropertyName("paths")]
    public SemgrepPaths? Paths { get; set; }
}

public class SemgrepResult
{
    [JsonPropertyName("check_id")]
    public string CheckId { get; set; } = string.Empty;
    
    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;
    
    [JsonPropertyName("start")]
    public SemgrepLocation Start { get; set; } = new();
    
    [JsonPropertyName("end")]
    public SemgrepLocation End { get; set; } = new();
    
    [JsonPropertyName("extra")]
    public SemgrepExtra Extra { get; set; } = new();
}

public class SemgrepLocation
{
    [JsonPropertyName("line")]
    public int Line { get; set; }
    
    [JsonPropertyName("col")]
    public int Col { get; set; }
}

public class SemgrepExtra
{
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
    
    [JsonPropertyName("severity")]
    public string Severity { get; set; } = "WARNING";
    
    [JsonPropertyName("metadata")]
    public Dictionary<string, JsonElement>? Metadata { get; set; }
    
    [JsonPropertyName("lines")]
    public string Lines { get; set; } = string.Empty;
    
    [JsonPropertyName("fix")]
    public string? Fix { get; set; }
}

public class SemgrepError
{
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
    
    [JsonPropertyName("path")]
    public string? Path { get; set; }
}

public class SemgrepPaths
{
    [JsonPropertyName("scanned")]
    public List<string> Scanned { get; set; } = new();
}

