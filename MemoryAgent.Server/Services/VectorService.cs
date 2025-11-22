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

    private const string FilesCollection = "files";
    private const string ClassesCollection = "classes";
    private const string MethodsCollection = "methods";
    private const string PatternsCollection = "patterns";

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

    public async Task InitializeCollectionsAsync(CancellationToken cancellationToken = default)
    {
        await CreateCollectionIfNotExistsAsync(FilesCollection, cancellationToken);
        await CreateCollectionIfNotExistsAsync(ClassesCollection, cancellationToken);
        await CreateCollectionIfNotExistsAsync(MethodsCollection, cancellationToken);
        await CreateCollectionIfNotExistsAsync(PatternsCollection, cancellationToken);

        _logger.LogInformation("Qdrant collections initialized successfully");
    }

    private async Task CreateCollectionIfNotExistsAsync(string collectionName, CancellationToken cancellationToken)
    {
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

            foreach (var group in grouped)
            {
                var collectionName = GetCollectionName(group.Key);
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
            var results = new List<CodeExample>();
            var collections = type.HasValue
                ? new[] { GetCollectionName(type.Value) }
                : new[] { FilesCollection, ClassesCollection, MethodsCollection, PatternsCollection };

            foreach (var collection in collections)
            {
                object? filter = null;
                if (!string.IsNullOrWhiteSpace(context))
                {
                    filter = new
                    {
                        must = new[]
                        {
                            new
                            {
                                key = "context",
                                match = new { value = context }
                            }
                        }
                    };
                }

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

    public async Task DeleteByFilePathAsync(string filePath, CancellationToken cancellationToken = default)
    {
        try
        {
            var collections = new[] { FilesCollection, ClassesCollection, MethodsCollection };

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

                response.EnsureSuccessStatusCode();
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

    private static string GetCollectionName(CodeMemoryType type) => type switch
    {
        CodeMemoryType.File => FilesCollection,
        CodeMemoryType.Class => ClassesCollection,
        CodeMemoryType.Method => MethodsCollection,
        CodeMemoryType.Property => MethodsCollection, // Properties go with methods
        CodeMemoryType.Interface => ClassesCollection, // Interfaces go with classes
        CodeMemoryType.Pattern => PatternsCollection,
        _ => throw new ArgumentException($"Unknown code memory type: {type}")
    };

    private static CodeMemoryType GetCodeMemoryType(string collection) => collection switch
    {
        FilesCollection => CodeMemoryType.File,
        ClassesCollection => CodeMemoryType.Class,
        MethodsCollection => CodeMemoryType.Method,
        PatternsCollection => CodeMemoryType.Pattern,
        _ => throw new ArgumentException($"Unknown collection: {collection}")
    };

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
}
