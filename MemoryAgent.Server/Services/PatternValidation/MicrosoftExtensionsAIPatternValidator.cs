using MemoryAgent.Server.Models;

namespace MemoryAgent.Server.Services.PatternValidation;

/// <summary>
/// Validator for Microsoft.Extensions.AI patterns
/// Covers IChatClient, IEmbeddingGenerator, tool calling, middleware, DI, and more
/// </summary>
public class MicrosoftExtensionsAIPatternValidator : IPatternValidator
{
    public IEnumerable<PatternType> SupportedPatternTypes => new[] { PatternType.MicrosoftExtensionsAI };

    public PatternQualityResult Validate(CodePattern pattern)
    {
        var result = new PatternQualityResult
        {
            Pattern = pattern,
            Score = 10,
            SecurityScore = 10
        };

        var implementation = pattern.Implementation.ToLower();
        var content = pattern.Content?.ToLower() ?? "";
        var name = pattern.Name.ToLower();

        // IChatClient patterns
        if (name.Contains("ichatclient"))
        {
            // Check for streaming support
            if (name.Contains("getstreaming"))
            {
                result.Recommendations.Add("✅ EXCELLENT: Using streaming responses for better UX");
            }
            
            // Check for error handling
            if (!content.Contains("try") && !content.Contains("catch"))
            {
                result.Issues.Add(new ValidationIssue
                {
                    Severity = IssueSeverity.Medium,
                    Category = IssueCategory.Reliability,
                    Message = "No error handling for chat requests - API calls can fail",
                    ScoreImpact = 2,
                    FixGuidance = "Wrap GetResponseAsync in try/catch to handle network/API errors"
                });
                result.Score -= 2;
            }
        }

        // Tool calling patterns
        if (name.Contains("aifunctionfactory") || name.Contains("functioninvocation"))
        {
            result.Recommendations.Add("✅ EXCELLENT: Using AIFunctionFactory for function calling");
            
            // Check for validation
            if (!content.Contains("validation") && !content.Contains("validate"))
            {
                result.Issues.Add(new ValidationIssue
                {
                    Severity = IssueSeverity.High,
                    Category = IssueCategory.Security,
                    Message = "No input validation for tool parameters - security risk",
                    ScoreImpact = 3,
                    FixGuidance = "Validate function parameters before invocation to prevent injection attacks",
                    SecurityReference = "OWASP-2021-A03"
                });
                result.Score -= 3;
                result.SecurityScore -= 2;
            }
        }

        // Middleware patterns
        if (name.Contains("delegating"))
        {
            result.Recommendations.Add("✅ EXCELLENT: Custom middleware for composable AI pipelines");
            
            // Check for proper disposal
            if (!content.Contains("dispose"))
            {
                result.Issues.Add(new ValidationIssue
                {
                    Severity = IssueSeverity.Medium,
                    Category = IssueCategory.Reliability,
                    Message = "Missing Dispose implementation in delegating client",
                    ScoreImpact = 2,
                    FixGuidance = "Override Dispose(bool disposing) to clean up resources"
                });
                result.Score -= 2;
            }
        }

        // Caching middleware
        if (name.Contains("distributedcache"))
        {
            result.Recommendations.Add("✅ EXCELLENT: Using distributed cache for response caching");
        }

        // Telemetry middleware
        if (name.Contains("opentelemetry"))
        {
            result.Recommendations.Add("✅ EXCELLENT: OpenTelemetry integration for observability");
        }

        // Dependency injection
        if (name.Contains("_di"))
        {
            result.Recommendations.Add("✅ EXCELLENT: Using DI for IChatClient/IEmbeddingGenerator");
            
            // Check for lifetime
            if (!content.Contains("singleton") && !content.Contains("scoped"))
            {
                result.Issues.Add(new ValidationIssue
                {
                    Severity = IssueSeverity.Low,
                    Category = IssueCategory.Performance,
                    Message = "Service lifetime not specified - consider Singleton for stateless clients",
                    ScoreImpact = 1,
                    FixGuidance = "Use AddSingleton for stateless AI clients to avoid recreation overhead"
                });
                result.Score -= 1;
            }
        }

        // ChatOptions configuration
        if (name.Contains("chatoptions"))
        {
            var hasTools = pattern.Metadata.TryGetValue("has_tools", out var toolsObj) && (bool)toolsObj;
            var hasTemperature = pattern.Metadata.TryGetValue("has_temperature", out var tempObj) && (bool)tempObj;
            
            if (!hasTemperature)
            {
                result.Recommendations.Add("Consider setting Temperature in ChatOptions to control response randomness");
            }
            
            if (!hasTools && content.Contains("function"))
            {
                result.Recommendations.Add("Add Tools property to ChatOptions for function calling");
            }
        }

        // Stateful conversation patterns
        if (name.Contains("statefulclient") || name.Contains("conversationid"))
        {
            result.Recommendations.Add("✅ EXCELLENT: Using ConversationId for stateful conversations");
            
            // Check if history is cleared when ConversationId is set
            if (name.Contains("conversationid") && !content.Contains("clear"))
            {
                result.Issues.Add(new ValidationIssue
                {
                    Severity = IssueSeverity.Medium,
                    Category = IssueCategory.Performance,
                    Message = "ConversationId set but history not cleared - may send duplicate messages to server",
                    ScoreImpact = 2,
                    FixGuidance = "Clear local history when ConversationId is set: if (response.ConversationId != null) { chatHistory.Clear(); }"
                });
                result.Score -= 2;
            }
        }

        // IImageGenerator patterns
        if (name.Contains("iimagegenerator"))
        {
            result.Recommendations.Add("✅ EXCELLENT: Using IImageGenerator for unified text-to-image generation");
            
            // Check for error handling
            if (!content.Contains("try") && !content.Contains("catch"))
            {
                result.Issues.Add(new ValidationIssue
                {
                    Severity = IssueSeverity.Medium,
                    Category = IssueCategory.Reliability,
                    Message = "No error handling for image generation - can fail due to content policy or API errors",
                    ScoreImpact = 2,
                    FixGuidance = "Wrap GenerateAsync in try/catch to handle content policy violations and API errors"
                });
                result.Score -= 2;
            }
        }

        // Structured output patterns
        if (name.Contains("structuredoutput") || name.Contains("responseformat"))
        {
            result.Recommendations.Add("✅ EXCELLENT: Using structured output with JSON schema for validated responses");
            
            // Check if validation is performed on the structured response
            if (!content.Contains("validation") && !content.Contains("validate"))
            {
                result.Recommendations.Add("Consider validating structured output against schema for data integrity");
            }
        }

        result.Score = Math.Max(0, result.Score);
        result.SecurityScore = Math.Max(0, result.SecurityScore);
        result.Summary = $"Microsoft.Extensions.AI Pattern Quality: {result.Grade} ({result.Score}/10), Security: {result.SecurityScore}/10";

        return result;
    }

    public PatternMigrationPath? GenerateMigrationPath(CodePattern pattern, bool includeCodeExample = true)
    {
        return null;
    }
}

