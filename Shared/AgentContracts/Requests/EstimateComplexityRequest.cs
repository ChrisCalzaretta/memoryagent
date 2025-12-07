namespace AgentContracts.Requests;

/// <summary>
/// Request to estimate task complexity and recommended iterations
/// </summary>
public class EstimateComplexityRequest
{
    /// <summary>
    /// The coding task to estimate
    /// </summary>
    public required string Task { get; set; }
    
    /// <summary>
    /// Optional language hint
    /// </summary>
    public string? Language { get; set; }
    
    /// <summary>
    /// Optional context from memory agent
    /// </summary>
    public string? Context { get; set; }
}

