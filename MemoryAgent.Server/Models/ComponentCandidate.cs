using System.Text.Json;
using System.Text.Json.Serialization;

namespace MemoryAgent.Server.Models;

/// <summary>
/// Candidate for component extraction (detected reusable pattern)
/// </summary>
public class ComponentCandidate
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;
    
    // Pattern detection
    [JsonPropertyName("occurrences")]
    public int Occurrences { get; set; }
    
    [JsonPropertyName("similarity")]
    public float Similarity { get; set; }
    
    [JsonPropertyName("locations")]
    public List<ComponentOccurrence> Locations { get; set; } = new();
    
    // Proposed component interface
    [JsonPropertyName("proposedInterface")]
    public ComponentInterface ProposedInterface { get; set; } = new();
    
    // Impact analysis
    [JsonPropertyName("linesSaved")]
    public int LinesSaved { get; set; }
    
    [JsonPropertyName("priority")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public Priority Priority { get; set; }
    
    [JsonPropertyName("valueScore")]
    public float ValueScore { get; set; }  // 0-100
    
    [JsonPropertyName("detectedAt")]
    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Location where component pattern was found
/// </summary>
public class ComponentOccurrence
{
    [JsonPropertyName("filePath")]
    public string FilePath { get; set; } = string.Empty;
    
    [JsonPropertyName("lineStart")]
    public int LineStart { get; set; }
    
    [JsonPropertyName("lineEnd")]
    public int LineEnd { get; set; }
    
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;
    
    // Use JsonElement for arbitrary JSON data - avoids Dictionary<string, object> deserialization issues
    [JsonPropertyName("metadata")]
    public Dictionary<string, JsonElement> Metadata { get; set; } = new();
}

/// <summary>
/// Proposed interface for extracted component
/// </summary>
public class ComponentInterface
{
    [JsonPropertyName("parameters")]
    public List<ComponentParameter> Parameters { get; set; } = new();
    
    [JsonPropertyName("events")]
    public List<ComponentEvent> Events { get; set; } = new();
    
    [JsonPropertyName("dependencies")]
    public List<string> Dependencies { get; set; } = new();
}

/// <summary>
/// Component parameter definition
/// </summary>
public class ComponentParameter
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;
    
    [JsonPropertyName("required")]
    public bool Required { get; set; }
    
    [JsonPropertyName("defaultValue")]
    public string? DefaultValue { get; set; }
    
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// Component event definition
/// </summary>
public class ComponentEvent
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;
    
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// Extracted component result
/// </summary>
public class ExtractedComponent
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    
    [JsonPropertyName("filePath")]
    public string FilePath { get; set; } = string.Empty;
    
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;
    
    [JsonPropertyName("css")]
    public string? CSS { get; set; }
    
    [JsonPropertyName("interface")]
    public ComponentInterface Interface { get; set; } = new();
    
    [JsonPropertyName("refactorings")]
    public List<ComponentRefactoring> Refactorings { get; set; } = new();
}

/// <summary>
/// Refactoring needed to use extracted component
/// </summary>
public class ComponentRefactoring
{
    [JsonPropertyName("filePath")]
    public string FilePath { get; set; } = string.Empty;
    
    [JsonPropertyName("lineStart")]
    public int LineStart { get; set; }
    
    [JsonPropertyName("lineEnd")]
    public int LineEnd { get; set; }
    
    [JsonPropertyName("oldCode")]
    public string OldCode { get; set; } = string.Empty;
    
    [JsonPropertyName("newCode")]
    public string NewCode { get; set; } = string.Empty;
}

/// <summary>
/// Priority levels for component extraction
/// </summary>
public enum Priority
{
    Low,
    Medium,
    High,
    Critical
}

