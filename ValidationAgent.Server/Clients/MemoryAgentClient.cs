using System.Net.Http.Json;
using System.Text.Json;

namespace ValidationAgent.Server.Clients;

/// <summary>
/// HTTP client for MemoryAgent.Server (Lightning)
/// </summary>
public class MemoryAgentClient : IMemoryAgentClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<MemoryAgentClient> _logger;
    
    // Prompt cache to reduce Lightning calls
    private readonly Dictionary<string, (PromptInfo Prompt, DateTime FetchedAt)> _promptCache = new();
    private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(5);
    
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public MemoryAgentClient(HttpClient httpClient, ILogger<MemoryAgentClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<PromptInfo?> GetPromptAsync(string promptName, CancellationToken cancellationToken)
    {
        // Check cache first
        if (_promptCache.TryGetValue(promptName, out var cached) &&
            DateTime.UtcNow - cached.FetchedAt < _cacheDuration)
        {
            _logger.LogDebug("Using cached prompt for {PromptName}", promptName);
            return cached.Prompt;
        }

        try
        {
            var request = new
            {
                name = "manage_prompts",
                arguments = new
                {
                    action = "list",
                    name = promptName,
                    activeOnly = true
                }
            };

            var response = await _httpClient.PostAsJsonAsync("/api/mcp/call", request, JsonOptions, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogDebug("Got prompt from Lightning: {Content}", content);
                
                var prompt = new PromptInfo
                {
                    Name = promptName,
                    Content = GetDefaultPrompt(promptName),
                    Version = 1,
                    IsActive = true
                };
                
                // Cache the result
                _promptCache[promptName] = (prompt, DateTime.UtcNow);
                return prompt;
            }

            _logger.LogWarning("Failed to get prompt {PromptName} from Lightning: {Status}", 
                promptName, response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error getting prompt {PromptName} from Lightning", promptName);
        }

        // Fallback to default
        return new PromptInfo
        {
            Name = promptName,
            Content = GetDefaultPrompt(promptName),
            Version = 0,
            IsActive = true
        };
    }

    public async Task<List<ValidationRuleInfo>> GetValidationRulesAsync(string context, CancellationToken cancellationToken)
    {
        try
        {
            var request = new
            {
                name = "validate",
                arguments = new
                {
                    context = context,
                    scope = "list_best_practices"
                }
            };

            var response = await _httpClient.PostAsJsonAsync("/api/mcp/call", request, JsonOptions, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogDebug("Got validation rules from Lightning: {Content}", content);
                
                // Parse MCP response - actual implementation would extract rules
                // For now, return default rules
                return GetDefaultValidationRules();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error getting validation rules from Lightning");
        }

        return GetDefaultValidationRules();
    }

    public async Task RecordPromptFeedbackAsync(
        string promptName, bool wasSuccessful, int? rating, CancellationToken cancellationToken)
    {
        try
        {
            var request = new
            {
                name = "feedback",
                arguments = new
                {
                    type = "prompt",
                    name = promptName,
                    wasSuccessful = wasSuccessful,
                    rating = rating
                }
            };

            await _httpClient.PostAsJsonAsync("/api/mcp/call", request, JsonOptions, cancellationToken);
            _logger.LogDebug("Recorded prompt feedback for {PromptName}: success={Success}, rating={Rating}",
                promptName, wasSuccessful, rating);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error recording prompt feedback for {PromptName}", promptName);
        }
    }

    public async Task RecordPatternFeedbackAsync(
        string patternName, bool wasUseful, string? comments, CancellationToken cancellationToken)
    {
        try
        {
            var request = new
            {
                name = "feedback",
                arguments = new
                {
                    type = "pattern",
                    patternName = patternName,
                    wasUseful = wasUseful,
                    comments = comments
                }
            };

            await _httpClient.PostAsJsonAsync("/api/mcp/call", request, JsonOptions, cancellationToken);
            _logger.LogDebug("Recorded pattern feedback for {PatternName}: useful={Useful}",
                patternName, wasUseful);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error recording pattern feedback for {PatternName}", patternName);
        }
    }

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken)
    {
        try
        {
            var response = await _httpClient.GetAsync("/health", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
    
    /// <summary>
    /// 🧠 MODEL LEARNING: Record model performance for future selection
    /// </summary>
    public async Task RecordModelPerformanceAsync(
        string model,
        string taskType,
        bool succeeded,
        double score,
        string? language = null,
        string? complexity = null,
        int iterations = 1,
        long durationMs = 0,
        string? errorType = null,
        List<string>? taskKeywords = null,
        string? context = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new
            {
                name = "store_model_performance",
                arguments = new
                {
                    model = model,
                    taskType = taskType,
                    language = language ?? "unknown",
                    complexity = complexity ?? "unknown",
                    outcome = succeeded ? "success" : (score > 0 ? "partial" : "failure"),
                    score = (int)score,
                    durationMs = durationMs,
                    iterations = iterations,
                    errorType = errorType ?? "",
                    context = context ?? "default",
                    taskKeywords = taskKeywords ?? new List<string>()
                }
            };

            var response = await _httpClient.PostAsJsonAsync("/api/mcp/call", request, JsonOptions, cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                _logger.LogDebug(
                    "📊 Recorded model performance: {Model} on {TaskType}/{Language} = {Outcome} ({Score}/10)",
                    model, taskType, language ?? "unknown", succeeded ? "success" : "failed", score);
            }
            else
            {
                _logger.LogWarning("Failed to record model performance: {Status}", response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error recording model performance for {Model} (non-critical)", model);
        }
    }
    
    /// <summary>
    /// 🧠 MODEL LEARNING: Get historical stats for models
    /// </summary>
    public async Task<List<ModelPerformanceStats>> GetModelStatsAsync(
        string? taskType,
        string? language,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new
            {
                jsonrpc = "2.0",
                method = "tools/call",
                @params = new
                {
                    name = "get_model_stats",
                    arguments = new
                    {
                        taskType = taskType ?? "",
                        language = language ?? ""
                    }
                },
                id = 1
            };

            var response = await _httpClient.PostAsJsonAsync("/api/mcp/call", request, JsonOptions, cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogDebug("Model stats response: {Content}", content);
                
                // Parse the markdown table response and extract stats
                return ParseModelStatsFromMarkdown(content);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error getting model stats");
        }
        
        return new List<ModelPerformanceStats>();
    }
    
    /// <summary>
    /// Parse model stats from the markdown table response
    /// </summary>
    private List<ModelPerformanceStats> ParseModelStatsFromMarkdown(string content)
    {
        var stats = new List<ModelPerformanceStats>();
        
        try
        {
            // The response contains a markdown table, extract the data
            // Format: | Model | Task Type | Success Rate | Avg Score | Samples |
            var lines = content.Split('\n');
            
            foreach (var line in lines)
            {
                if (line.StartsWith('|') && !line.Contains("---") && !line.Contains("Model"))
                {
                    var parts = line.Split('|', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 5)
                    {
                        var stat = new ModelPerformanceStats
                        {
                            Model = parts[0].Trim(),
                            TaskType = parts[1].Trim(),
                            Language = "all" // Not in current format
                        };
                        
                        // Parse success rate (e.g., "85%")
                        if (double.TryParse(parts[2].Trim().TrimEnd('%'), out var successRate))
                        {
                            stat.TotalAttempts = 100; // Estimate
                            stat.Successes = (int)(successRate);
                        }
                        
                        // Parse avg score
                        if (double.TryParse(parts[3].Trim(), out var avgScore))
                        {
                            stat.AverageScore = avgScore;
                        }
                        
                        // Parse samples
                        if (int.TryParse(parts[4].Trim(), out var samples))
                        {
                            stat.TotalAttempts = samples;
                            stat.Successes = (int)(successRate / 100 * samples);
                        }
                        
                        stats.Add(stat);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error parsing model stats markdown");
        }
        
        return stats;
    }

    private static string GetDefaultPrompt(string promptName) => promptName switch
    {
        "validation_agent_system" => @"You are an expert code reviewer. Your task is to review code for quality, security, and best practices.

VALIDATION RULES:
1. Check for null reference vulnerabilities
2. Check for proper error handling
3. Check for security issues (SQL injection, hardcoded secrets, etc.)
4. Check for proper async patterns
5. Check for proper resource disposal
6. Check for code maintainability
7. Check for proper naming conventions

SCORING:
- 10: Perfect, no issues
- 8-9: Good, minor suggestions only
- 6-7: Acceptable, needs some fixes
- 4-5: Poor, significant issues
- 0-3: Critical, major problems

Be strict but fair. Focus on real issues, not style preferences.",

        "validation_agent_security" => @"You are a security-focused code reviewer. Your primary concern is finding security vulnerabilities.

CRITICAL SECURITY CHECKS:
1. SQL Injection - Check for parameterized queries
2. XSS - Check for output encoding
3. CSRF - Check for anti-forgery tokens
4. Hardcoded secrets - Check for API keys, passwords in code
5. Insecure deserialization
6. Path traversal vulnerabilities
7. Improper authentication/authorization

Score HARSHLY for security issues. A single critical vulnerability should result in score < 5.",

        _ => "You are a helpful AI code reviewer."
    };

    private static List<ValidationRuleInfo> GetDefaultValidationRules() => new()
    {
        new ValidationRuleInfo
        {
            Name = "null-check",
            Description = "Check for null reference vulnerabilities",
            Severity = "high",
            CheckPattern = "Missing null check before property access",
            FixSuggestion = "Add null check: if (obj != null) or use null-conditional: obj?.Property"
        },
        new ValidationRuleInfo
        {
            Name = "error-handling",
            Description = "Check for proper error handling",
            Severity = "medium",
            CheckPattern = "Missing try-catch or empty catch blocks",
            FixSuggestion = "Add appropriate try-catch with logging"
        },
        new ValidationRuleInfo
        {
            Name = "async-pattern",
            Description = "Check for proper async/await patterns",
            Severity = "medium",
            CheckPattern = "Missing async keyword or .Result/.Wait() usage",
            FixSuggestion = "Use async/await instead of blocking calls"
        },
        new ValidationRuleInfo
        {
            Name = "resource-disposal",
            Description = "Check for proper resource disposal",
            Severity = "high",
            CheckPattern = "IDisposable without using statement",
            FixSuggestion = "Use 'using' statement or implement IDisposable pattern"
        },
        new ValidationRuleInfo
        {
            Name = "security-sql",
            Description = "Check for SQL injection vulnerabilities",
            Severity = "critical",
            CheckPattern = "String concatenation in SQL queries",
            FixSuggestion = "Use parameterized queries"
        },
        new ValidationRuleInfo
        {
            Name = "security-secrets",
            Description = "Check for hardcoded secrets",
            Severity = "critical",
            CheckPattern = "API keys, passwords, connection strings in code",
            FixSuggestion = "Use configuration or secret managers"
        }
    };
}



