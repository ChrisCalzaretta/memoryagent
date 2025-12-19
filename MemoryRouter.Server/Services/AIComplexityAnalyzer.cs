using System.Text;
using System.Text.Json;

namespace MemoryRouter.Server.Services;

/// <summary>
/// Uses DeepSeek AI to analyze task complexity and predict execution time
/// Provides intelligent estimates even for first-time requests
/// </summary>
public class AIComplexityAnalyzer : IAIComplexityAnalyzer
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AIComplexityAnalyzer> _logger;
    
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public AIComplexityAnalyzer(HttpClient httpClient, ILogger<AIComplexityAnalyzer> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<ExecutionPrediction> PredictExecutionAsync(
        string toolName,
        string userRequest,
        Dictionary<string, object> arguments,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("🤖 AI analyzing: {Tool} for request: {Request}", toolName, userRequest);

        try
        {
            var prompt = BuildAnalysisPrompt(toolName, userRequest, arguments);

            var request = new
            {
                model = "deepseek-coder-v2:16b",
                prompt = prompt,
                format = "json",
                stream = false,
                options = new
                {
                    temperature = 0.1, // Low temp for consistent predictions
                    top_p = 0.8
                }
            };

            var requestJson = JsonSerializer.Serialize(request, _jsonOptions);
            var content = new StringContent(requestJson, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("/api/generate", content, cancellationToken);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            var ollamaResponse = JsonSerializer.Deserialize<OllamaResponse>(responseJson, _jsonOptions);

            if (ollamaResponse?.Response == null)
            {
                throw new InvalidOperationException("AI returned empty response");
            }

            var prediction = ParsePrediction(ollamaResponse.Response, toolName);

            _logger.LogInformation("🤖 AI Prediction: {Complexity} complexity, {Seconds}s, confidence: {Confidence}%", 
                prediction.Complexity, prediction.EstimatedSeconds, prediction.ConfidencePercent);
            _logger.LogDebug("   💭 Reasoning: {Reasoning}", prediction.Reasoning);

            return prediction;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ AI analysis failed, using conservative estimate");
            
            // Return conservative estimate if AI fails
            return new ExecutionPrediction
            {
                Complexity = TaskComplexity.Medium,
                EstimatedSeconds = 30,
                ShouldRunAsync = true,
                ConfidencePercent = 50,
                Reasoning = $"AI analysis failed: {ex.Message}. Using conservative estimate."
            };
        }
    }

    private string BuildAnalysisPrompt(string toolName, string userRequest, Dictionary<string, object> arguments)
    {
        var sb = new StringBuilder();

        sb.AppendLine("You are an expert at predicting software development task complexity and execution time.");
        sb.AppendLine();
        sb.AppendLine("## Task to Analyze:");
        sb.AppendLine($"Tool: {toolName}");
        sb.AppendLine($"User Request: \"{userRequest}\"");
        sb.AppendLine($"Arguments: {JsonSerializer.Serialize(arguments)}");
        sb.AppendLine();
        sb.AppendLine("## Analysis Guidelines:");
        sb.AppendLine();
        sb.AppendLine("### LOW Complexity (5-15 seconds):");
        sb.AppendLine("- Simple CRUD operations");
        sb.AppendLine("- Hello World / Basic examples");
        sb.AppendLine("- Single file modifications");
        sb.AppendLine("- Simple data structures");
        sb.AppendLine();
        sb.AppendLine("### MEDIUM Complexity (15-60 seconds):");
        sb.AppendLine("- REST APIs with 3-5 endpoints");
        sb.AppendLine("- Basic web applications");
        sb.AppendLine("- Standard design patterns");
        sb.AppendLine("- Multiple related files");
        sb.AppendLine();
        sb.AppendLine("### HIGH Complexity (60-180 seconds):");
        sb.AppendLine("- Microservices architecture");
        sb.AppendLine("- Full-stack applications");
        sb.AppendLine("- Complex business logic");
        sb.AppendLine("- Multiple integrations");
        sb.AppendLine("- Database design + migrations");
        sb.AppendLine();
        sb.AppendLine("## Response Format:");
        sb.AppendLine("Respond with ONLY valid JSON:");
        sb.AppendLine(@"{
  ""complexity"": ""low|medium|high"",
  ""estimatedSeconds"": 30,
  ""confidencePercent"": 85,
  ""reasoning"": ""Clear explanation of your analysis"",
  ""shouldRunAsync"": true,
  ""keyFactors"": [""factor1"", ""factor2""]
}");
        sb.AppendLine();
        sb.AppendLine("Analyze the task and provide your prediction:");

        return sb.ToString();
    }

    private ExecutionPrediction ParsePrediction(string response, string toolName)
    {
        try
        {
            // Clean response
            response = response.Trim();
            if (response.StartsWith("```json"))
            {
                response = response[7..];
            }
            if (response.StartsWith("```"))
            {
                response = response[3..];
            }
            if (response.EndsWith("```"))
            {
                response = response[..^3];
            }

            var parsed = JsonSerializer.Deserialize<AIPredictionResponse>(response, _jsonOptions);

            if (parsed == null)
            {
                throw new InvalidOperationException("Failed to parse AI response");
            }

            var complexity = parsed.Complexity?.ToLowerInvariant() switch
            {
                "low" => TaskComplexity.Low,
                "medium" => TaskComplexity.Medium,
                "high" => TaskComplexity.High,
                _ => TaskComplexity.Medium
            };

            return new ExecutionPrediction
            {
                Complexity = complexity,
                EstimatedSeconds = parsed.EstimatedSeconds ?? 30,
                ShouldRunAsync = parsed.ShouldRunAsync ?? (parsed.EstimatedSeconds > 15),
                ConfidencePercent = parsed.ConfidencePercent ?? 75,
                Reasoning = parsed.Reasoning ?? "No reasoning provided",
                KeyFactors = parsed.KeyFactors ?? new List<string>()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse AI prediction: {Response}", response);
            throw;
        }
    }

    private class OllamaResponse
    {
        public string? Response { get; set; }
    }

    private class AIPredictionResponse
    {
        public string? Complexity { get; set; }
        public int? EstimatedSeconds { get; set; }
        public int? ConfidencePercent { get; set; }
        public string? Reasoning { get; set; }
        public bool? ShouldRunAsync { get; set; }
        public List<string>? KeyFactors { get; set; }
    }
}

public interface IAIComplexityAnalyzer
{
    Task<ExecutionPrediction> PredictExecutionAsync(
        string toolName,
        string userRequest,
        Dictionary<string, object> arguments,
        CancellationToken cancellationToken = default);
}

public enum TaskComplexity
{
    Low,     // 5-15 seconds
    Medium,  // 15-60 seconds
    High     // 60+ seconds
}

public class ExecutionPrediction
{
    public TaskComplexity Complexity { get; set; }
    public int EstimatedSeconds { get; set; }
    public bool ShouldRunAsync { get; set; }
    public int ConfidencePercent { get; set; }
    public string Reasoning { get; set; } = string.Empty;
    public List<string> KeyFactors { get; set; } = new();
}

