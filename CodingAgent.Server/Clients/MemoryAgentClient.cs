using System.Text;
using System.Text.Json;

namespace CodingAgent.Server.Clients;

/// <summary>
/// Implementation of MemoryAgent client
/// Calls MemoryAgent MCP server to access Qdrant and Neo4j
/// </summary>
public class MemoryAgentClient : IMemoryAgentClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<MemoryAgentClient> _logger;
    private readonly string _memoryAgentUrl;
    
    public MemoryAgentClient(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<MemoryAgentClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _memoryAgentUrl = configuration["MemoryAgent:BaseUrl"] ?? configuration["MemoryAgent:Url"] ?? "http://memory-agent-server:5000";
        
        _logger.LogInformation("MemoryAgentClient configured with URL: {Url}", _memoryAgentUrl);
    }
    
    public async Task<List<SearchResult>> SmartSearchAsync(string query, int limit = 5, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("🔍 Calling MemoryAgent smart search: {Query}", query);
            
            var request = new
            {
                tool = "smartsearch",
                query = query,
                limit = limit
            };
            
            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PostAsync($"{_memoryAgentUrl}/mcp", content, cancellationToken);
            response.EnsureSuccessStatusCode();
            
            var resultJson = await response.Content.ReadAsStringAsync(cancellationToken);
            var results = JsonSerializer.Deserialize<SmartSearchResponse>(resultJson);
            
            return results?.Results?.Select(r => new SearchResult
            {
                Path = r.Path ?? "",
                Snippet = r.Content ?? "",
                Score = r.Score
            }).ToList() ?? new List<SearchResult>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Smart search failed for query: {Query}", query);
            return new List<SearchResult>();
        }
    }
    
    public async Task<List<RelatedFile>> GetCoEditedFilesAsync(string filePath, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("🔗 Getting co-edited files for: {Path}", filePath);
            
            var request = new
            {
                tool = "get_coedited_files",
                file_path = filePath,
                limit = 5
            };
            
            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PostAsync($"{_memoryAgentUrl}/mcp", content, cancellationToken);
            response.EnsureSuccessStatusCode();
            
            var resultJson = await response.Content.ReadAsStringAsync(cancellationToken);
            var results = JsonSerializer.Deserialize<CoEditedFilesResponse>(resultJson);
            
            return results?.Files?.Select(f => new RelatedFile
            {
                Path = f.Path ?? "",
                Score = f.Score,
                Relationship = "co-edited"
            }).ToList() ?? new List<RelatedFile>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Get co-edited files failed for: {Path}", filePath);
            return new List<RelatedFile>();
        }
    }
    
    public async Task<List<string>> GetFileDependenciesAsync(string filePath, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("📦 Getting dependencies for: {Path}", filePath);
            
            var request = new
            {
                tool = "dependency_chain",
                file_path = filePath
            };
            
            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PostAsync($"{_memoryAgentUrl}/mcp", content, cancellationToken);
            response.EnsureSuccessStatusCode();
            
            var resultJson = await response.Content.ReadAsStringAsync(cancellationToken);
            var results = JsonSerializer.Deserialize<DependencyChainResponse>(resultJson);
            
            return results?.Dependencies ?? new List<string>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Get dependencies failed for: {Path}", filePath);
            return new List<string>();
        }
    }
    
    // Response DTOs for deserialization
    
    private class SmartSearchResponse
    {
        public List<SmartSearchResult>? Results { get; set; }
    }
    
    private class SmartSearchResult
    {
        public string? Path { get; set; }
        public string? Content { get; set; }
        public double Score { get; set; }
    }
    
    private class CoEditedFilesResponse
    {
        public List<CoEditedFile>? Files { get; set; }
    }
    
    private class CoEditedFile
    {
        public string? Path { get; set; }
        public double Score { get; set; }
    }
    
    private class DependencyChainResponse
    {
        public List<string>? Dependencies { get; set; }
    }
    
    public async Task IndexFileAsync(string filePath, string content, string? context = null, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("📇 Indexing file in MemoryAgent: {Path}", filePath);
            
            var request = new
            {
                tool = "index",
                scope = "file",
                path = filePath,
                content = content,
                context = context ?? "codegen"
            };
            
            var json = JsonSerializer.Serialize(request);
            var httpContent = new StringContent(json, Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PostAsync($"{_memoryAgentUrl}/mcp", httpContent, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("⚠️ MemoryAgent indexing failed (HTTP {Status}): {Error}", response.StatusCode, error);
            }
            else
            {
                _logger.LogDebug("✅ File indexed successfully: {Path}", filePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Index file failed for: {Path}", filePath);
            // Don't throw - indexing is optional, code generation should continue
        }
    }
    
    public async Task IndexFilesAsync(List<(string Path, string Content)> files, string? context = null, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("📇 Bulk indexing {Count} files in MemoryAgent", files.Count);
            
            var request = new
            {
                tool = "index",
                scope = "directory",
                files = files.Select(f => new { path = f.Path, content = f.Content }).ToList(),
                context = context ?? "codegen"
            };
            
            var json = JsonSerializer.Serialize(request);
            var httpContent = new StringContent(json, Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PostAsync($"{_memoryAgentUrl}/mcp", httpContent, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("⚠️ MemoryAgent bulk indexing failed (HTTP {Status}): {Error}", response.StatusCode, error);
            }
            else
            {
                _logger.LogDebug("✅ {Count} files indexed successfully", files.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Bulk index failed for {Count} files", files.Count);
            // Don't throw - indexing is optional
        }
    }
    
    public async Task StorePromptAsync(Services.PromptMetadata prompt, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("💾 Storing prompt in MemoryAgent: {Id}", prompt.Id);
            
            // JSON-RPC 2.0 format for MCP
            var request = new
            {
                jsonrpc = "2.0",
                method = "tools/call",
                @params = new
                {
                    name = "manage_prompts",
                    arguments = new
                    {
                        action = "create",
                        prompt_id = prompt.Id,
                        name = prompt.Name,
                        category = prompt.Category,
                        content = prompt.Content,
                        version = prompt.Version,
                        tags = prompt.Tags,
                        context = prompt.Context
                    }
                },
                id = 1
            };
            
            var json = JsonSerializer.Serialize(request);
            _logger.LogDebug("📤 Storing prompt '{Id}': {Json}", prompt.Id, json.Length > 300 ? json.Substring(0, 300) + "..." : json);
            
            var httpContent = new StringContent(json, Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PostAsync($"{_memoryAgentUrl}/mcp", httpContent, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("❌ Failed to store prompt '{Id}': HTTP {Status}, Body: {Body}", 
                    prompt.Id, response.StatusCode, responseBody);
                throw new HttpRequestException($"MemoryAgent returned {response.StatusCode}: {responseBody}");
            }
            
            _logger.LogInformation("✅ Prompt stored: {Id}, Response: {Response}", prompt.Id, 
                responseBody.Length > 200 ? responseBody.Substring(0, 200) + "..." : responseBody);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to store prompt: {Id}", prompt.Id);
            throw;
        }
    }
    
    public async Task<Services.PromptMetadata?> GetPromptAsync(string promptId, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("🔍 Getting prompt from MemoryAgent: {Id}", promptId);
            
            // JSON-RPC 2.0 format for MCP
            var request = new
            {
                jsonrpc = "2.0",
                method = "tools/call",
                @params = new
                {
                    name = "manage_prompts",
                    arguments = new
                    {
                        action = "get",
                        prompt_id = promptId
                    }
                },
                id = 1
            };
            
            var json = JsonSerializer.Serialize(request);
            _logger.LogDebug("📤 Sending to MemoryAgent: {Json}", json);
            
            var httpContent = new StringContent(json, Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PostAsync($"{_memoryAgentUrl}/mcp", httpContent, cancellationToken);
            
            var resultJson = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogInformation("📥 MemoryAgent response for '{Id}': Status={Status}, Body={Body}", 
                promptId, response.StatusCode, resultJson.Length > 500 ? resultJson.Substring(0, 500) + "..." : resultJson);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("⚠️ Failed to get prompt '{Id}': HTTP {Status}", promptId, response.StatusCode);
                return null;
            }
            
            // Parse JSON-RPC response
            var jsonRpcResponse = JsonSerializer.Deserialize<JsonRpcResponse>(resultJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            
            if (jsonRpcResponse?.Result == null || !jsonRpcResponse.Result.HasValue)
            {
                _logger.LogWarning("⚠️ Prompt '{Id}' returned null result from MemoryAgent", promptId);
                return null;
            }
            
            // Extract the prompt from the result
            var resultValue = jsonRpcResponse.Result.Value;
            if (!resultValue.TryGetProperty("prompt", out var promptData))
            {
                _logger.LogWarning("⚠️ Prompt '{Id}' result missing 'prompt' property", promptId);
                return null;
            }
            
            var result = JsonSerializer.Deserialize<PromptDto>(promptData.GetRawText(), new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            
            if (result == null)
            {
                _logger.LogWarning("⚠️ Prompt '{Id}' returned null from MemoryAgent (result structure issue?)", promptId);
                return null;
            }
            
            return new Services.PromptMetadata
            {
                Id = result.Id ?? promptId,
                Name = result.Name ?? "",
                Category = result.Category ?? "",
                Content = result.Content ?? "",
                Version = result.Version,
                Tags = result.Tags ?? new List<string>(),
                Context = result.Context ?? "",
                SuccessRate = result.SuccessRate,
                UsageCount = result.UsageCount,
                AvgScore = result.AvgScore,
                AvgIterations = result.AvgIterations
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get prompt: {Id}", promptId);
            return null;
        }
    }
    
    public async Task RecordPromptFeedbackAsync(string promptId, Services.PromptUsageResult result, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("📊 Recording prompt feedback: {Id}, Score: {Score}", promptId, result.Score);
            
            var request = new
            {
                tool = "feedback",
                type = "prompt",
                prompt_id = promptId,
                success = result.Success,
                score = result.Score,
                iterations = result.Iterations,
                issues = result.Issues,
                metadata = result.Metadata
            };
            
            var json = JsonSerializer.Serialize(request);
            var httpContent = new StringContent(json, Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PostAsync($"{_memoryAgentUrl}/mcp", httpContent, cancellationToken);
            response.EnsureSuccessStatusCode();
            
            _logger.LogDebug("✅ Feedback recorded for prompt: {Id}", promptId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to record prompt feedback: {Id}", promptId);
            // Don't throw - feedback is optional
        }
    }
    
    public async Task StoreQAAsync(string question, string answer, int score, string language, Dictionary<string, object>? metadata = null, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("💾 Storing Q&A in MemoryAgent: {Question}", question.Substring(0, Math.Min(50, question.Length)));
            
            var request = new
            {
                tool = "store_qa",
                question = question,
                answer = answer,
                score = score,
                language = language,
                metadata = metadata ?? new Dictionary<string, object>()
            };
            
            var json = JsonSerializer.Serialize(request);
            var httpContent = new StringContent(json, Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PostAsync($"{_memoryAgentUrl}/mcp", httpContent, cancellationToken);
            response.EnsureSuccessStatusCode();
            
            _logger.LogDebug("✅ Q&A stored");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to store Q&A");
            // Don't throw - storage is optional
        }
    }
    
    public async Task<List<QAPair>> FindSimilarQuestionsAsync(string question, int limit = 5, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("🔍 Finding similar questions: {Question}", question.Substring(0, Math.Min(50, question.Length)));
            
            var request = new
            {
                tool = "find_similar_questions",
                question = question,
                limit = limit
            };
            
            var json = JsonSerializer.Serialize(request);
            var httpContent = new StringContent(json, Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PostAsync($"{_memoryAgentUrl}/mcp", httpContent, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                return new List<QAPair>();
            }
            
            var resultJson = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<SimilarQuestionsResponse>(resultJson);
            
            return result?.Questions ?? new List<QAPair>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to find similar questions");
            return new List<QAPair>();
        }
    }
    
    // Response DTOs
    
    private class JsonRpcResponse
    {
        public string? Jsonrpc { get; set; }
        public JsonElement? Result { get; set; }
        public JsonRpcError? Error { get; set; }
        public int Id { get; set; }
    }
    
    private class JsonRpcError
    {
        public int Code { get; set; }
        public string? Message { get; set; }
    }
    
    private class PromptDto
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? Category { get; set; }
        public string? Content { get; set; }
        public int Version { get; set; }
        public List<string>? Tags { get; set; }
        public string? Context { get; set; }
        public double SuccessRate { get; set; }
        public int UsageCount { get; set; }
        public double AvgScore { get; set; }
        public double AvgIterations { get; set; }
    }
    
    private class SimilarQuestionsResponse
    {
        public List<QAPair>? Questions { get; set; }
    }
    
    public async Task<LightningContext> GetContextAsync(string workspacePath, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("🧠 Initializing Lightning context for: {Path}", workspacePath);
            
            // Extract context name from workspace path (e.g., "/workspace/testagent" → "testagent")
            var contextName = Path.GetFileName(workspacePath.TrimEnd('/', '\\'));
            
            var request = new
            {
                tool = "get_context",
                context = contextName
            };
            
            var json = JsonSerializer.Serialize(request);
            var httpContent = new StringContent(json, Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PostAsync($"{_memoryAgentUrl}/mcp", httpContent, cancellationToken);
            response.EnsureSuccessStatusCode();
            
            var resultJson = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<ContextResponse>(resultJson);
            
            var context = new LightningContext
            {
                ContextName = contextName,
                WorkspacePath = workspacePath,
                SessionStarted = DateTime.UtcNow,
                DiscussedFiles = result?.DiscussedFiles ?? new List<string>(),
                Metadata = result?.Metadata ?? new Dictionary<string, object>()
            };
            
            _logger.LogInformation("✅ Lightning context initialized: {Context}, {FileCount} discussed files",
                contextName, context.DiscussedFiles.Count);
            
            return context;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get Lightning context for: {Path}", workspacePath);
            
            // Return empty context as fallback
            return new LightningContext
            {
                ContextName = Path.GetFileName(workspacePath.TrimEnd('/', '\\')),
                WorkspacePath = workspacePath,
                SessionStarted = DateTime.UtcNow
            };
        }
    }
    
    public async Task<WorkspaceStatus> GetWorkspaceStatusAsync(string? context = null, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("📊 Getting workspace status from Lightning");
            
            var request = new
            {
                tool = "workspace_status",
                context = context
            };
            
            var json = JsonSerializer.Serialize(request);
            var httpContent = new StringContent(json, Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PostAsync($"{_memoryAgentUrl}/mcp", httpContent, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                return new WorkspaceStatus();
            }
            
            var resultJson = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<WorkspaceStatusResponse>(resultJson);
            
            return new WorkspaceStatus
            {
                WorkspacePath = result?.WorkspacePath ?? "",
                RecentFiles = result?.RecentFiles ?? new List<string>(),
                ImportantFiles = result?.ImportantFiles ?? new List<string>(),
                Recommendations = result?.Recommendations ?? new List<string>(),
                TotalFilesIndexed = result?.TotalFilesIndexed ?? 0,
                LastActivity = result?.LastActivity ?? DateTime.UtcNow,
                LanguageBreakdown = result?.LanguageBreakdown ?? new Dictionary<string, int>()
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get workspace status");
            return new WorkspaceStatus();
        }
    }
    
    public async Task RecordFileEditedAsync(string filePath, string context, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("📝 Recording file edit in Lightning: {Path}", filePath);
            
            var request = new
            {
                tool = "record_file_edited",
                file_path = filePath,
                context = context
            };
            
            var json = JsonSerializer.Serialize(request);
            var httpContent = new StringContent(json, Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PostAsync($"{_memoryAgentUrl}/mcp", httpContent, cancellationToken);
            
            // Don't throw - this is optional tracking
            if (response.IsSuccessStatusCode)
            {
                _logger.LogDebug("✅ File edit recorded: {Path}", filePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to record file edit: {Path}", filePath);
        }
    }
    
    public async Task<List<string>> GetRecommendationsAsync(string? context = null, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("💡 Getting recommendations from Lightning");
            
            var request = new
            {
                tool = "get_recommendations",
                context = context
            };
            
            var json = JsonSerializer.Serialize(request);
            var httpContent = new StringContent(json, Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PostAsync($"{_memoryAgentUrl}/mcp", httpContent, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                return new List<string>();
            }
            
            var resultJson = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<RecommendationsResponse>(resultJson);
            
            return result?.Recommendations ?? new List<string>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get recommendations");
            return new List<string>();
        }
    }
    
    public async Task<List<string>> GetImportantFilesAsync(string workspacePath, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("📁 Getting important files from Lightning");
            
            var request = new
            {
                tool = "get_important_files",
                workspace_path = workspacePath
            };
            
            var json = JsonSerializer.Serialize(request);
            var httpContent = new StringContent(json, Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PostAsync($"{_memoryAgentUrl}/mcp", httpContent, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                return new List<string>();
            }
            
            var resultJson = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<ImportantFilesResponse>(resultJson);
            
            return result?.Files ?? new List<string>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get important files");
            return new List<string>();
        }
    }
    
    // Additional response DTOs
    
    private class ContextResponse
    {
        public List<string>? DiscussedFiles { get; set; }
        public Dictionary<string, object>? Metadata { get; set; }
    }
    
    private class WorkspaceStatusResponse
    {
        public string? WorkspacePath { get; set; }
        public List<string>? RecentFiles { get; set; }
        public List<string>? ImportantFiles { get; set; }
        public List<string>? Recommendations { get; set; }
        public int TotalFilesIndexed { get; set; }
        public DateTime LastActivity { get; set; }
        public Dictionary<string, int>? LanguageBreakdown { get; set; }
    }
    
    private class RecommendationsResponse
    {
        public List<string>? Recommendations { get; set; }
    }
    
    private class ImportantFilesResponse
    {
        public List<string>? Files { get; set; }
    }
    
    public async Task<string?> CallMcpToolAsync(string toolName, Dictionary<string, object> arguments, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("🔧 Calling MCP tool: {Tool}", toolName);
            
            var mcpRequest = new
            {
                jsonrpc = "2.0",
                id = 1,
                method = "tools/call",
                @params = new
                {
                    name = toolName,
                    arguments = arguments
                }
            };
            
            var json = JsonSerializer.Serialize(mcpRequest);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PostAsync($"{_memoryAgentUrl}/mcp", content, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("⚠️ MCP tool call failed: {Tool}, Status: {Status}", toolName, response.StatusCode);
                return null;
            }
            
            var resultJson = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogDebug("📥 MCP response for '{Tool}': {Response}", toolName, resultJson);
            
            return resultJson;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ MCP tool call failed: {Tool}", toolName);
            return null;
        }
    }
}
