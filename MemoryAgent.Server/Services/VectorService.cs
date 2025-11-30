using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MemoryAgent.Server.Models;
using Polly;
using Polly.Retry;

namespace MemoryAgent.Server.Services;

/// <summary>
/// Service for interacting with Qdrant vector database via HTTP REST API
/// </summary>
public class VectorService : IVectorService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<VectorService> _logger;
    private readonly int _vectorSize;
    private readonly AsyncRetryPolicy _retryPolicy;
    private readonly string _baseUrl;

    // Helper methods for per-workspace collection names
    private static string GetFilesCollection(string? context) => 
        string.IsNullOrWhiteSpace(context) ? "files" : $"{context.ToLower()}_files";
    
    private static string GetClassesCollection(string? context) => 
        string.IsNullOrWhiteSpace(context) ? "classes" : $"{context.ToLower()}_classes";
    
    private static string GetMethodsCollection(string? context) => 
        string.IsNullOrWhiteSpace(context) ? "methods" : $"{context.ToLower()}_methods";
    
    private static string GetPatternsCollection(string? context) => 
        string.IsNullOrWhiteSpace(context) ? "patterns" : $"{context.ToLower()}_patterns";

    public VectorService(
        IConfiguration configuration,
        ILogger<VectorService> logger,
        HttpClient httpClient)
    {
        var qdrantUrl = configuration["Qdrant:Url"] ?? "http://localhost:6333";
        
        // Ensure URL uses HTTP port (6333) not gRPC port (6334)
        if (qdrantUrl.Contains(":6334"))
        {
            qdrantUrl = qdrantUrl.Replace(":6334", ":6333");
            logger.LogWarning("Changed Qdrant URL from gRPC port 6334 to HTTP port 6333");
        }
        
        _baseUrl = qdrantUrl.TrimEnd('/');
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri(_baseUrl);
        _httpClient.Timeout = TimeSpan.FromMinutes(5);
        
        _logger = logger;
        _vectorSize = int.Parse(configuration["Embedding:Dimension"] ?? "1024");

        _retryPolicy = Policy
            .Handle<Exception>()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                onRetry: (exception, timeSpan, retryCount, context) =>
                {
                    _logger.LogWarning(
                        "Retry {RetryCount} after {Delay}s due to: {Exception}",
                        retryCount, timeSpan.TotalSeconds, exception.Message);
                });
    }

    /// <summary>
    /// DEPRECATED: Do not use. Collections should only be created per-workspace.
    /// Use InitializeCollectionsForContextAsync(context) instead.
    /// </summary>
    [Obsolete("Collections must be created per-workspace via register_workspace MCP tool")]
    public async Task InitializeCollectionsAsync(CancellationToken cancellationToken = default)
    {
        // DEPRECATED: Collections are now workspace-specific only
        // This method is kept for interface compatibility but does nothing
        _logger.LogWarning("⚠️ InitializeCollectionsAsync called but collections are now workspace-specific only");
        _logger.LogWarning("   Use register_workspace MCP tool to create per-workspace collections");
        
        await Task.CompletedTask;
    }

    /// <summary>
    /// Initialize collections for a specific workspace context
    /// </summary>
    public async Task InitializeCollectionsForContextAsync(string context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(context))
        {
            _logger.LogWarning("Cannot initialize collections for empty context");
            return;
        }

        var normalized = context.ToLower();
        _logger.LogInformation("Initializing Qdrant collections for context: {Context}", normalized);

        await CreateCollectionIfNotExistsAsync(GetFilesCollection(context), cancellationToken);
        await CreateCollectionIfNotExistsAsync(GetClassesCollection(context), cancellationToken);
        await CreateCollectionIfNotExistsAsync(GetMethodsCollection(context), cancellationToken);
        await CreateCollectionIfNotExistsAsync(GetPatternsCollection(context), cancellationToken);

        _logger.LogInformation("✅ Qdrant collections initialized for {Context}: {Files}, {Classes}, {Methods}, {Patterns}",
            normalized,
            GetFilesCollection(context),
            GetClassesCollection(context),
            GetMethodsCollection(context),
            GetPatternsCollection(context));
    }

    private async Task CreateCollectionIfNotExistsAsync(string collectionName, CancellationToken cancellationToken)
    {
        // GUARD: Only create collections with a workspace context prefix
        // Collection names should be like: workspacename_classes, workspacename_methods
        // Reject: classes, methods, files, patterns (no workspace context)
        if (!collectionName.Contains('_'))
        {
            _logger.LogWarning("⚠️ Skipping collection creation without workspace context: {Collection}", collectionName);
            _logger.LogWarning("   Collections must be created per-workspace via register_workspace MCP tool");
            return;
        }
        
        try
        {
            // Check if collection exists
            var response = await _httpClient.GetAsync($"/collections/{collectionName}", cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Collection {Collection} already exists", collectionName);
                return;
            }

            // Create collection
            var createRequest = new
            {
                vectors = new
                {
                    size = _vectorSize,
                    distance = "Cosine"
                }
            };

            var json = JsonSerializer.Serialize(createRequest);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            response = await _httpClient.PutAsync($"/collections/{collectionName}", content, cancellationToken);
            response.EnsureSuccessStatusCode();

            _logger.LogInformation("Created collection {Collection}", collectionName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating collection {Collection}", collectionName);
            throw;
        }
    }

    public async Task StoreCodeMemoryAsync(CodeMemory memory, CancellationToken cancellationToken = default)
    {
        await StoreCodeMemoriesAsync(new List<CodeMemory> { memory }, cancellationToken);
    }

    public async Task StoreCodeMemoriesAsync(List<CodeMemory> memories, CancellationToken cancellationToken = default)
    {
        if (!memories.Any())
            return;

        try
        {
            // Group by type for batch insertion
            var grouped = memories.GroupBy(m => m.Type);

            // Extract context from first memory (all should have same context)
            var memoryContext = memories.FirstOrDefault()?.Context;

            foreach (var group in grouped)
            {
                var collectionName = GetCollectionName(group.Key, memoryContext);
                var points = new List<object>();

                foreach (var memory in group)
                {
                    if (memory.Embedding == null || memory.Embedding.Length == 0)
                    {
                        _logger.LogWarning("Skipping {Type} {Name} - no embedding", memory.Type, memory.Name);
                        continue;
                    }

                    var payload = new Dictionary<string, object>
                    {
                        ["name"] = memory.Name,
                        ["content"] = memory.Content,
                        ["file_path"] = memory.FilePath,
                        ["context"] = memory.Context,
                        ["line_number"] = memory.LineNumber,
                        ["indexed_at"] = memory.IndexedAt.ToString("O")
                    };

                    // Add metadata
                    foreach (var (key, value) in memory.Metadata)
                    {
                        payload[key] = value;
                    }

                    var point = new
                    {
                        id = Guid.NewGuid().ToString(),
                        vector = memory.Embedding,
                        payload
                    };

                    points.Add(point);
                }

                if (points.Any())
                {
                    await _retryPolicy.ExecuteAsync(async () =>
                    {
                        var upsertRequest = new { points };
                        var json = JsonSerializer.Serialize(upsertRequest);
                        var content = new StringContent(json, Encoding.UTF8, "application/json");
                        
                        var response = await _httpClient.PutAsync(
                            $"/collections/{collectionName}/points?wait=true", 
                            content, 
                            cancellationToken);
                        
                        response.EnsureSuccessStatusCode();
                    });

                    _logger.LogInformation(
                        "Stored {Count} {Type} memories in {Collection}",
                        points.Count, group.Key, collectionName);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error storing code memories");
            throw;
        }
    }

    public async Task<List<CodeExample>> SearchSimilarCodeAsync(
        float[] queryEmbedding,
        CodeMemoryType? type = null,
        string? context = null,
        int limit = 5,
        float minimumScore = 0.7f,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Ensure collections exist before searching
            if (!string.IsNullOrWhiteSpace(context))
            {
                await InitializeCollectionsForContextAsync(context, cancellationToken);
            }
            
            var results = new List<CodeExample>();
            var collections = type.HasValue
                ? new[] { GetCollectionName(type.Value, context) }
                : new[] { GetFilesCollection(context), GetClassesCollection(context), GetMethodsCollection(context), GetPatternsCollection(context) };

            foreach (var collection in collections)
            {
                // No need for context filtering anymore since collections are per-context
                object? filter = null;

                var searchRequest = new
                {
                    vector = queryEmbedding,
                    limit,
                    score_threshold = minimumScore,
                    filter,
                    with_payload = true
                };

                var json = JsonSerializer.Serialize(searchRequest, new JsonSerializerOptions 
                { 
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull 
                });
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(
                    $"/collections/{collection}/points/search",
                    content,
                    cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Search failed for collection {Collection}: {Status}", 
                        collection, response.StatusCode);
                    continue;
                }

                var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
                var searchResponse = JsonSerializer.Deserialize<QdrantSearchResponse>(responseJson);

                if (searchResponse?.Result != null)
                {
                    foreach (var result in searchResponse.Result)
                    {
                        if (result.Payload == null) continue;

                        var example = new CodeExample
                        {
                            Name = GetPayloadString(result.Payload, "name"),
                            Code = GetPayloadString(result.Payload, "content"),
                            FilePath = GetPayloadString(result.Payload, "file_path"),
                            Context = GetPayloadString(result.Payload, "context"),
                            LineNumber = GetPayloadInt(result.Payload, "line_number"),
                            Score = result.Score,
                            Type = GetCodeMemoryType(collection)
                        };

                        // Add other metadata
                        foreach (var kvp in result.Payload)
                        {
                            if (!new[] { "name", "content", "file_path", "context", "line_number", "indexed_at" }.Contains(kvp.Key))
                            {
                                example.Metadata[kvp.Key] = kvp.Value;
                            }
                        }

                        results.Add(example);
                    }
                }
            }

            return results
                .OrderByDescending(r => r.Score)
                .Take(limit)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching similar code");
            throw;
        }
    }

    public async Task DeleteByFilePathAsync(string filePath, string? context = null, CancellationToken cancellationToken = default)
    {
        try
        {
            // Ensure collections exist before deleting
            if (!string.IsNullOrWhiteSpace(context))
            {
                await InitializeCollectionsForContextAsync(context, cancellationToken);
            }
            
            var collections = new[] { GetFilesCollection(context), GetClassesCollection(context), GetMethodsCollection(context) };

            foreach (var collection in collections)
            {
                var deleteRequest = new
                {
                    filter = new
                    {
                        must = new[]
                        {
                            new
                            {
                                key = "file_path",
                                match = new { value = filePath }
                            }
                        }
                    }
                };

                var json = JsonSerializer.Serialize(deleteRequest);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(
                    $"/collections/{collection}/points/delete?wait=true",
                    content,
                    cancellationToken);

                // 404 is OK - means no points to delete (empty collection or no matching points)
                if (!response.IsSuccessStatusCode && response.StatusCode != System.Net.HttpStatusCode.NotFound)
                {
                    response.EnsureSuccessStatusCode();
                }
            }

            _logger.LogInformation("Deleted memories for file: {FilePath}", filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting memories for file: {FilePath}", filePath);
            throw;
        }
    }

    public async Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("/collections", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check failed for Qdrant");
            return false;
        }
    }

    public async Task<List<string>> GetFilePathsForContextAsync(string? context = null, CancellationToken cancellationToken = default)
    {
        // Ensure collections exist before querying
        if (!string.IsNullOrWhiteSpace(context))
        {
            await InitializeCollectionsForContextAsync(context, cancellationToken);
        }
        
        var filePaths = new HashSet<string>();
        var collections = new[] { GetFilesCollection(context), GetClassesCollection(context), GetMethodsCollection(context), GetPatternsCollection(context) };

        try
        {
            foreach (var collection in collections)
            {
                // Scroll through all points in the collection
                // No filter needed since collections are per-context
                var scrollRequest = new
                {
                    limit = 1000, // Scroll in batches
                    with_payload = new[] { "file_path" },
                    with_vector = false
                };

                var scrollJson = JsonSerializer.Serialize(scrollRequest);
                var scrollContent = new StringContent(scrollJson, Encoding.UTF8, "application/json");

                var scrollResponse = await _httpClient.PostAsync($"/collections/{collection}/points/scroll", scrollContent, cancellationToken);
                
                if (scrollResponse.IsSuccessStatusCode)
                {
                    var scrollResult = await scrollResponse.Content.ReadAsStringAsync(cancellationToken);
                    var scrollData = JsonSerializer.Deserialize<QdrantScrollResponse>(scrollResult);

                    if (scrollData?.Result?.Points != null)
                    {
                        foreach (var point in scrollData.Result.Points)
                        {
                            if (point.Payload != null && point.Payload.TryGetValue("file_path", out var filePathElement))
                            {
                                var filePath = filePathElement.ValueKind == JsonValueKind.String 
                                    ? filePathElement.GetString() 
                                    : filePathElement.ToString();
                                
                                if (!string.IsNullOrEmpty(filePath))
                                {
                                    filePaths.Add(filePath);
                                }
                            }
                        }
                    }
                }
            }

            return filePaths.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting file paths for context: {Context}", context);
            return new List<string>();
        }
    }

    public async Task<DateTime?> GetFileLastIndexedTimeAsync(string filePath, CancellationToken cancellationToken = default)
    {
        try
        {
            // Search for the file in the files collection
            var searchRequest = new
            {
                filter = new
                {
                    must = new[]
                    {
                        new
                        {
                            key = "file_path",
                            match = new { value = filePath }
                        }
                    }
                },
                limit = 1,
                with_payload = new[] { "indexed_at" },
                with_vector = false
            };

            var searchJson = JsonSerializer.Serialize(searchRequest);
            var searchContent = new StringContent(searchJson, Encoding.UTF8, "application/json");

            // Extract context from filePath if needed (for per-workspace collections)
            // For now, assume context is embedded in filePath or passed separately
            var searchResponse = await _httpClient.PostAsync($"/collections/{GetFilesCollection(null)}/points/scroll", searchContent, cancellationToken);

            if (searchResponse.IsSuccessStatusCode)
            {
                var searchResult = await searchResponse.Content.ReadAsStringAsync(cancellationToken);
                var searchData = JsonSerializer.Deserialize<QdrantScrollResponse>(searchResult);

                if (searchData?.Result?.Points != null && searchData.Result.Points.Count > 0)
                {
                    var point = searchData.Result.Points[0];
                    if (point.Payload != null && point.Payload.TryGetValue("indexed_at", out var indexedAtElement))
                    {
                        var indexedAtStr = indexedAtElement.ValueKind == JsonValueKind.String 
                            ? indexedAtElement.GetString() 
                            : indexedAtElement.ToString();

                        if (DateTime.TryParse(indexedAtStr, out var indexedAt))
                        {
                            return indexedAt;
                        }
                    }
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting last indexed time for file: {FilePath}", filePath);
            return null;
        }
    }

    private static string GetCollectionName(CodeMemoryType type, string? context) => type switch
    {
        CodeMemoryType.File => GetFilesCollection(context),
        CodeMemoryType.Class => GetClassesCollection(context),
        CodeMemoryType.Method => GetMethodsCollection(context),
        CodeMemoryType.Property => GetMethodsCollection(context), // Properties go with methods
        CodeMemoryType.Interface => GetClassesCollection(context), // Interfaces go with classes
        CodeMemoryType.Pattern => GetPatternsCollection(context),
        _ => throw new ArgumentException($"Unknown code memory type: {type}")
    };

    private static CodeMemoryType GetCodeMemoryType(string collection)
    {
        // Remove context prefix if present (e.g., "memoryagent_files" -> "files")
        var parts = collection.Split('_');
        var baseName = parts.Length > 1 ? parts[^1] : collection;
        
        return baseName switch
        {
            "files" => CodeMemoryType.File,
            "classes" => CodeMemoryType.Class,
            "methods" => CodeMemoryType.Method,
            "patterns" => CodeMemoryType.Pattern,
            _ => throw new ArgumentException($"Unknown collection: {collection}")
        };
    }

    private static string GetPayloadString(Dictionary<string, JsonElement> payload, string key)
    {
        if (payload.TryGetValue(key, out var value))
        {
            return value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : value.ToString();
        }
        return "";
    }

    private static int GetPayloadInt(Dictionary<string, JsonElement> payload, string key)
    {
        if (payload.TryGetValue(key, out var value))
        {
            if (value.ValueKind == JsonValueKind.Number)
                return value.GetInt32();
            if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out var intVal))
                return intVal;
        }
        return 0;
    }

    // DTOs for Qdrant HTTP API responses
    private class QdrantSearchResponse
    {
        [JsonPropertyName("result")]
        public List<QdrantSearchResult>? Result { get; set; }
    }

    private class QdrantSearchResult
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("score")]
        public float Score { get; set; }

        [JsonPropertyName("payload")]
        public Dictionary<string, JsonElement>? Payload { get; set; }
    }

    private class QdrantScrollResponse
    {
        [JsonPropertyName("result")]
        public QdrantScrollResult? Result { get; set; }
    }

    private class QdrantScrollResult
    {
        [JsonPropertyName("points")]
        public List<QdrantPoint>? Points { get; set; }

        [JsonPropertyName("next_page_offset")]
        public string? NextPageOffset { get; set; }
    }

    private class QdrantPoint
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("payload")]
        public Dictionary<string, JsonElement>? Payload { get; set; }
    }
}
