namespace AgentContracts.Responses;

/// <summary>
/// Response with task complexity estimation
/// </summary>
public class EstimateComplexityResponse
{
    /// <summary>
    /// Whether estimation succeeded
    /// </summary>
    public bool Success { get; set; }
    
    /// <summary>
    /// Complexity level: simple, moderate, complex, very_complex
    /// </summary>
    public string ComplexityLevel { get; set; } = "moderate";
    
    /// <summary>
    /// Recommended number of iterations (will be clamped 12-50)
    /// </summary>
    public int RecommendedIterations { get; set; } = 12;
    
    /// <summary>
    /// Estimated number of files to generate
    /// </summary>
    public int EstimatedFiles { get; set; } = 1;
    
    /// <summary>
    /// Brief reasoning for the estimate
    /// </summary>
    public string Reasoning { get; set; } = "";
    
    /// <summary>
    /// Error message if estimation failed
    /// </summary>
    public string? Error { get; set; }
}










