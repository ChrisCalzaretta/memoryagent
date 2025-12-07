using System.ComponentModel.DataAnnotations;

namespace AgentContracts.Requests;

/// <summary>
/// Request to estimate task complexity and recommended iterations
/// </summary>
public class EstimateComplexityRequest
{
    /// <summary>
    /// The coding task to estimate
    /// </summary>
    [Required(ErrorMessage = "Task description is required")]
    [StringLength(10000, MinimumLength = 5, ErrorMessage = "Task must be between 5 and 10000 characters")]
    public required string Task { get; set; }
    
    /// <summary>
    /// Optional language hint
    /// </summary>
    [RegularExpression(@"^(python|csharp|typescript|javascript|go|rust|java|ruby|php|swift|kotlin|dart)?$",
        ErrorMessage = "Invalid language specified")]
    public string? Language { get; set; }
    
    /// <summary>
    /// Optional context from memory agent
    /// </summary>
    [StringLength(200, ErrorMessage = "Context cannot exceed 200 characters")]
    public string? Context { get; set; }
}

