using System.Text.Json;
using MemoryAgent.Server.Models;

namespace MemoryAgent.Server.Services;

/// <summary>
/// LLM-powered service for classifying user intent from natural language
/// Uses DeepSeek Coder via Ollama for intelligent classification
/// </summary>
public class IntentClassificationService : IIntentClassificationService
{
    private readonly ILLMService _llmService;
    private readonly ILogger<IntentClassificationService> _logger;

    public IntentClassificationService(
        ILLMService llmService,
        ILogger<IntentClassificationService> logger)
    {
        _llmService = llmService;
        _logger = logger;
    }

    public async Task<UserIntent> ClassifyIntentAsync(
        string userRequest,
        string? context = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("🧠 Classifying user intent: '{Request}'", userRequest);

            var prompt = BuildIntentClassificationPrompt(userRequest, context);
            var llmResponse = await _llmService.GenerateAsync(prompt, cancellationToken);

            _logger.LogDebug("LLM Response: {Response}", llmResponse);

            // Parse JSON response
            var intent = ParseIntentResponse(llmResponse, userRequest);

            _logger.LogInformation("✅ Intent classified: {ProjectType} / {Goal} / Confidence: {Confidence:P0}", 
                intent.ProjectType, intent.PrimaryGoal, intent.Confidence);

            return intent;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error classifying intent for: {Request}", userRequest);
            
            // Fallback to simple keyword-based classification
            return FallbackClassification(userRequest, context);
        }
    }

    public async Task<List<PatternCategory>> SuggestPatternCategoriesAsync(
        UserIntent intent,
        CancellationToken cancellationToken = default)
    {
        var categories = new List<PatternCategory>();

        // Add relevant categories based on intent
        if (intent.RelevantCategories.Any())
        {
            categories.AddRange(intent.RelevantCategories);
        }

        // Goal-based category mapping
        categories.AddRange(intent.PrimaryGoal switch
        {
            UserGoal.Performance => new[] { PatternCategory.Performance },
            UserGoal.Security => new[] { PatternCategory.Security, PatternCategory.Validation, PatternCategory.SecurityPatterns },
            UserGoal.Refactoring => new[] { PatternCategory.CodeQuality, PatternCategory.DesignImplementation },
            UserGoal.NewFeature => new[] { PatternCategory.AIAgents, PatternCategory.ToolIntegration },
            UserGoal.Testing => new[] { PatternCategory.Testing, PatternCategory.CodeQuality },
            UserGoal.Observability => new[] { PatternCategory.Observability, PatternCategory.Operational },
            UserGoal.Scalability => new[] { PatternCategory.Performance, PatternCategory.ResiliencyPatterns, PatternCategory.DistributedSystems },
            _ => Array.Empty<PatternCategory>()
        });

        // ProjectType-based category mapping
        categories.AddRange(intent.ProjectType switch
        {
            ProjectType.MobileApp when intent.Technologies.Any(t => t.Contains("Flutter")) 
                => new[] { PatternCategory.ComponentModel, PatternCategory.StateManagement, PatternCategory.UserExperience },
            ProjectType.AIAgent => new[] { PatternCategory.AIAgents, PatternCategory.MultiAgentOrchestration, PatternCategory.ToolIntegration },
            ProjectType.WebAPI => new[] { PatternCategory.ResiliencyPatterns, PatternCategory.Validation, PatternCategory.Security },
            ProjectType.WebApp when intent.Technologies.Any(t => t.Contains("Blazor"))
                => new[] { PatternCategory.ComponentModel, PatternCategory.Lifecycle, PatternCategory.DataBinding },
            _ => Array.Empty<PatternCategory>()
        });

        return categories.Distinct().ToList();
    }

    public async Task<List<string>> SuggestBestPracticesAsync(
        UserIntent intent,
        CancellationToken cancellationToken = default)
    {
        var practices = new List<string>();

        // Technology-specific practices
        if (intent.Technologies.Contains("Flutter") || intent.Technologies.Contains("Dart"))
        {
            practices.AddRange(new[]
            {
                "flutter-stateless-widgets",
                "flutter-lazy-list-builder",
                "dart-null-safety",
                "dart-async-await",
                "flutter-dispose-controllers"
            });
        }

        if (intent.Technologies.Contains("Microsoft.Extensions.AI") || intent.Technologies.Contains("AI"))
        {
            practices.AddRange(new[]
            {
                "meai-ichatclient-interface",
                "meai-streaming-responses",
                "meai-function-calling",
                "meai-opentelemetry-middleware",
                "meai-tool-input-validation"
            });
        }

        if (intent.Technologies.Contains("Blazor"))
        {
            practices.AddRange(new[]
            {
                "blazor-component-lifecycle",
                "blazor-data-binding",
                "blazor-form-validation"
            });
        }

        // Goal-specific practices
        if (intent.PrimaryGoal == UserGoal.Security)
        {
            practices.AddRange(new[]
            {
                "input-validation",
                "authentication",
                "authorization",
                "secure-storage"
            });
        }

        if (intent.PrimaryGoal == UserGoal.Performance)
        {
            practices.AddRange(new[]
            {
                "cache-aside",
                "distributed-cache",
                "response-cache"
            });
        }

        return practices.Distinct().ToList();
    }

    #region Private Helpers

    private string BuildIntentClassificationPrompt(string userRequest, string? context)
    {
        return $@"You are an expert software architect analyzing user intent.

USER REQUEST: ""{userRequest}""
{(context != null ? $"PROJECT CONTEXT: {context}" : "")}

Analyze this request and classify the user's intent. Return ONLY valid JSON, no markdown, no explanation.

JSON Schema:
{{
  ""projectType"": ""MobileApp | WebAPI | AIAgent | WebApp | DesktopApp | BackendService | Library | DataPipeline | MicroService | Unknown"",
  ""primaryGoal"": ""Performance | Security | Refactoring | NewFeature | BugFix | Migration | Testing | Observability | Scalability | CostOptimization | Unknown"",
  ""technologies"": [""Flutter"", ""Dart"", ""CSharp"", ""Python"", ""React"", ""Blazor"", ""AI"", etc.],
  ""relevantCategories"": [""Performance"", ""Security"", ""AIAgents"", ""StateManagement"", etc.],
  ""domain"": ""ecommerce | healthcare | fintech | general | etc."",
  ""complexity"": ""Simple | Medium | Complex | Enterprise"",
  ""confidence"": 0.0-1.0,
  ""reasoning"": ""Brief explanation""
}}

EXAMPLES:

Request: ""Build secure Flutter e-commerce app""
Response: {{""projectType"":""MobileApp"",""primaryGoal"":""NewFeature"",""technologies"":[""Flutter"",""Dart""],""relevantCategories"":[""Security"",""StateManagement"",""UIUX""],""domain"":""ecommerce"",""complexity"":""Complex"",""confidence"":0.95}}

Request: ""Add caching to UserService""
Response: {{""projectType"":""BackendService"",""primaryGoal"":""Performance"",""technologies"":[""CSharp""],""relevantCategories"":[""Performance"",""Caching""],""domain"":""general"",""complexity"":""Simple"",""confidence"":0.90}}

Request: ""Migrate from Semantic Kernel to Microsoft.Extensions.AI""
Response: {{""projectType"":""AIAgent"",""primaryGoal"":""Migration"",""technologies"":[""CSharp"",""AI"",""Microsoft.Extensions.AI""],""relevantCategories"":[""AIAgents"",""ToolIntegration""],""domain"":""general"",""complexity"":""Medium"",""confidence"":0.95}}

Now classify this request:

USER REQUEST: ""{userRequest}""

Return ONLY the JSON object:";
    }

    private UserIntent ParseIntentResponse(string llmResponse, string originalRequest)
    {
        try
        {
            // Extract JSON from response (remove markdown code blocks if present)
            var jsonText = llmResponse.Trim();
            
            // Remove markdown code fences
            if (jsonText.StartsWith("```"))
            {
                var lines = jsonText.Split('\n');
                jsonText = string.Join('\n', lines.Skip(1).Take(lines.Length - 2));
            }

            // Parse JSON
            var jsonDoc = JsonDocument.Parse(jsonText);
            var root = jsonDoc.RootElement;

            var intent = new UserIntent
            {
                OriginalRequest = originalRequest,
                ProjectType = ParseEnum<ProjectType>(root, "projectType"),
                PrimaryGoal = ParseEnum<UserGoal>(root, "primaryGoal"),
                Technologies = ParseStringArray(root, "technologies"),
                Domain = root.TryGetProperty("domain", out var domain) ? domain.GetString() : null,
                Complexity = ParseEnum<ComplexityLevel>(root, "complexity"),
                Confidence = root.TryGetProperty("confidence", out var conf) ? (float)conf.GetDouble() : 0.5f
            };

            // Parse relevant categories
            if (root.TryGetProperty("relevantCategories", out var categories))
            {
                foreach (var cat in categories.EnumerateArray())
                {
                    if (Enum.TryParse<PatternCategory>(cat.GetString(), out var category))
                    {
                        intent.RelevantCategories.Add(category);
                    }
                }
            }

            // Store reasoning in metadata
            if (root.TryGetProperty("reasoning", out var reasoning))
            {
                intent.Metadata["reasoning"] = reasoning.GetString() ?? "";
            }

            return intent;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse LLM response, using fallback classification");
            return FallbackClassification(originalRequest, null);
        }
    }

    private T ParseEnum<T>(JsonElement root, string propertyName) where T : struct, Enum
    {
        if (root.TryGetProperty(propertyName, out var prop))
        {
            var value = prop.GetString();
            if (Enum.TryParse<T>(value, true, out var result))
            {
                return result;
            }
        }
        return default;
    }

    private List<string> ParseStringArray(JsonElement root, string propertyName)
    {
        var result = new List<string>();
        if (root.TryGetProperty(propertyName, out var array))
        {
            foreach (var item in array.EnumerateArray())
            {
                var value = item.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    result.Add(value);
                }
            }
        }
        return result;
    }

    private UserIntent FallbackClassification(string userRequest, string? context)
    {
        _logger.LogInformation("Using fallback keyword-based classification");

        var intent = new UserIntent
        {
            OriginalRequest = userRequest,
            Confidence = 0.5f
        };

        var lowerRequest = userRequest.ToLowerInvariant();

        // Detect project type
        if (lowerRequest.Contains("flutter") || lowerRequest.Contains("mobile") || lowerRequest.Contains("app"))
            intent.ProjectType = ProjectType.MobileApp;
        else if (lowerRequest.Contains("api") || lowerRequest.Contains("endpoint"))
            intent.ProjectType = ProjectType.WebAPI;
        else if (lowerRequest.Contains("agent") || lowerRequest.Contains("llm") || lowerRequest.Contains("chat"))
            intent.ProjectType = ProjectType.AIAgent;
        else if (lowerRequest.Contains("blazor") || lowerRequest.Contains("web"))
            intent.ProjectType = ProjectType.WebApp;

        // Detect goal
        if (lowerRequest.Contains("secure") || lowerRequest.Contains("security") || lowerRequest.Contains("auth"))
            intent.PrimaryGoal = UserGoal.Security;
        else if (lowerRequest.Contains("performance") || lowerRequest.Contains("fast") || lowerRequest.Contains("cache"))
            intent.PrimaryGoal = UserGoal.Performance;
        else if (lowerRequest.Contains("refactor") || lowerRequest.Contains("clean") || lowerRequest.Contains("improve"))
            intent.PrimaryGoal = UserGoal.Refactoring;
        else if (lowerRequest.Contains("migrate") || lowerRequest.Contains("upgrade"))
            intent.PrimaryGoal = UserGoal.Migration;
        else if (lowerRequest.Contains("test") || lowerRequest.Contains("testing"))
            intent.PrimaryGoal = UserGoal.Testing;
        else
            intent.PrimaryGoal = UserGoal.NewFeature;

        // Detect technologies
        if (lowerRequest.Contains("flutter")) intent.Technologies.Add("Flutter");
        if (lowerRequest.Contains("dart")) intent.Technologies.Add("Dart");
        if (lowerRequest.Contains("c#") || lowerRequest.Contains("csharp")) intent.Technologies.Add("CSharp");
        if (lowerRequest.Contains("python")) intent.Technologies.Add("Python");
        if (lowerRequest.Contains("blazor")) intent.Technologies.Add("Blazor");
        if (lowerRequest.Contains("react")) intent.Technologies.Add("React");
        if (lowerRequest.Contains("ai") || lowerRequest.Contains("llm") || lowerRequest.Contains("chat"))
            intent.Technologies.Add("AI");
        if (lowerRequest.Contains("microsoft.extensions.ai")) intent.Technologies.Add("Microsoft.Extensions.AI");

        // Detect domain
        if (lowerRequest.Contains("ecommerce") || lowerRequest.Contains("e-commerce") || lowerRequest.Contains("shop"))
            intent.Domain = "ecommerce";
        else if (lowerRequest.Contains("healthcare") || lowerRequest.Contains("health") || lowerRequest.Contains("medical"))
            intent.Domain = "healthcare";
        else if (lowerRequest.Contains("fintech") || lowerRequest.Contains("finance") || lowerRequest.Contains("bank"))
            intent.Domain = "fintech";

        // Map to pattern categories
        intent.RelevantCategories = MapToPatternCategories(intent);

        return intent;
    }

    private List<PatternCategory> MapToPatternCategories(UserIntent intent)
    {
        var categories = new List<PatternCategory>();

        // Map from goal
        switch (intent.PrimaryGoal)
        {
            case UserGoal.Performance:
                categories.AddRange(new[] { PatternCategory.Performance });
                break;
            case UserGoal.Security:
                categories.AddRange(new[] { PatternCategory.Security, PatternCategory.Validation });
                break;
            case UserGoal.Observability:
                categories.AddRange(new[] { PatternCategory.Observability, PatternCategory.Operational });
                break;
        }

        // Map from project type
        switch (intent.ProjectType)
        {
            case ProjectType.MobileApp:
                categories.AddRange(new[] { PatternCategory.ComponentModel, PatternCategory.StateManagement });
                break;
            case ProjectType.AIAgent:
                categories.AddRange(new[] { PatternCategory.AIAgents, PatternCategory.ToolIntegration });
                break;
            case ProjectType.WebAPI:
                categories.AddRange(new[] { PatternCategory.ResiliencyPatterns, PatternCategory.Security });
                break;
        }

        return categories.Distinct().ToList();
    }

    #endregion
}

