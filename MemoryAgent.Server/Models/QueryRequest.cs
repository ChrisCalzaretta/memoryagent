namespace MemoryAgent.Server.Models;

/// <summary>
/// Request to query code memory
/// </summary>
public class QueryRequest
{
    public string Query { get; set; } = string.Empty;
    public string? Context { get; set; }
    public int Limit { get; set; } = 5;
    public float MinimumScore { get; set; } = 0.7f;
}

public class QueryResult
{
    public List<CodeExample> Results { get; set; } = new();
    public TimeSpan Duration { get; set; }
}

public class CodeExample
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string Context { get; set; } = string.Empty;
    public int LineNumber { get; set; }
    public float Score { get; set; }
    public CodeMemoryType Type { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

