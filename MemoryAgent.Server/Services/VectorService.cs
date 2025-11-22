using MemoryAgent.Server.Models;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using Polly;
using Polly.Retry;

namespace MemoryAgent.Server.Services;

/// <summary>
/// Service for interacting with Qdrant vector database
/// </summary>
public class VectorService : IVectorService
{
    private readonly QdrantClient _client;
    private readonly ILogger<VectorService> _logger;
    private readonly int _vectorSize;
    private readonly AsyncRetryPolicy _retryPolicy;

    private const string FilesCollection = "files";
    private const string ClassesCollection = "classes";
    private const string MethodsCollection = "methods";
    private const string PatternsCollection = "patterns";

    public VectorService(
        IConfiguration configuration,
        ILogger<VectorService> logger)
    {
        var qdrantUrl = configuration["Qdrant:Url"] ?? "http://localhost:6333";
        
        // Parse URL to extract host and port
        var uri = new Uri(qdrantUrl);
        var host = uri.Host;
        var port = uri.Port;
        var useHttps = uri.Scheme == "https";
        
        _client = new QdrantClient(host, port, useHttps);
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
            var collections = await _client.ListCollectionsAsync(cancellationToken);
            
            if (collections.Any(c => c == collectionName))
            {
                _logger.LogInformation("Collection {Collection} already exists", collectionName);
                return;
            }

            await _client.CreateCollectionAsync(
                collectionName,
                new VectorParams
                {
                    Size = (ulong)_vectorSize,
                    Distance = Distance.Cosine
                },
                cancellationToken: cancellationToken);

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
                var points = new List<PointStruct>();

                foreach (var memory in group)
                {
                    if (memory.Embedding == null || memory.Embedding.Length == 0)
                    {
                        _logger.LogWarning("Skipping {Type} {Name} - no embedding", memory.Type, memory.Name);
                        continue;
                    }

                    var point = new PointStruct
                    {
                        Id = Guid.NewGuid(),
                        Vectors = memory.Embedding,
                        Payload =
                        {
                            ["name"] = memory.Name,
                            ["content"] = memory.Content,
                            ["file_path"] = memory.FilePath,
                            ["context"] = memory.Context,
                            ["line_number"] = memory.LineNumber,
                            ["indexed_at"] = memory.IndexedAt.ToString("O")
                        }
                    };

                    // Add metadata
                    foreach (var (key, value) in memory.Metadata)
                    {
                        // Qdrant payload accepts standard types directly
                        if (value is string str)
                            point.Payload[key] = str;
                        else if (value is int intVal)
                            point.Payload[key] = intVal;
                        else if (value is long longVal)
                            point.Payload[key] = longVal;
                        else if (value is double doubleVal)
                            point.Payload[key] = doubleVal;
                        else if (value is float floatVal)
                            point.Payload[key] = (double)floatVal;
                        else if (value is bool boolVal)
                            point.Payload[key] = boolVal;
                        else
                            point.Payload[key] = value.ToString() ?? "";
                    }

                    points.Add(point);
                }

                if (points.Any())
                {
                    await _retryPolicy.ExecuteAsync(async () =>
                    {
                        await _client.UpsertAsync(collectionName, points, cancellationToken: cancellationToken);
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
                Filter? filter = null;
                if (!string.IsNullOrWhiteSpace(context))
                {
                    filter = new Filter
                    {
                        Must =
                        {
                            new Condition
                            {
                                Field = new FieldCondition
                                {
                                    Key = "context",
                                    Match = new Match { Keyword = context }
                                }
                            }
                        }
                    };
                }

                var searchResults = await _client.SearchAsync(
                    collection,
                    queryEmbedding,
                    limit: (ulong)limit,
                    scoreThreshold: minimumScore,
                    filter: filter,
                    cancellationToken: cancellationToken);

                foreach (var result in searchResults)
                {
                    var example = new CodeExample
                    {
                        Name = result.Payload["name"].StringValue,
                        Code = result.Payload["content"].StringValue,
                        FilePath = result.Payload["file_path"].StringValue,
                        Context = result.Payload["context"].StringValue,
                        LineNumber = (int)result.Payload["line_number"].IntegerValue,
                        Score = result.Score,
                        Type = GetCodeMemoryType(collection)
                    };

                    // Add other metadata
                    foreach (var kvp in result.Payload)
                    {
                        if (!new[] { "name", "content", "file_path", "context", "line_number", "indexed_at" }.Contains(kvp.Key))
                        {
                            // Convert Qdrant Value to object
                            object metadataValue = kvp.Value.KindCase switch
                            {
                                Value.KindOneofCase.StringValue => kvp.Value.StringValue,
                                Value.KindOneofCase.IntegerValue => kvp.Value.IntegerValue,
                                Value.KindOneofCase.DoubleValue => kvp.Value.DoubleValue,
                                Value.KindOneofCase.BoolValue => kvp.Value.BoolValue,
                                _ => kvp.Value.ToString()
                            };
                            example.Metadata[kvp.Key] = metadataValue;
                        }
                    }

                    results.Add(example);
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
                var filter = new Filter
                {
                    Must =
                    {
                        new Condition
                        {
                            Field = new FieldCondition
                            {
                                Key = "file_path",
                                Match = new Match { Keyword = filePath }
                            }
                        }
                    }
                };

                await _client.DeleteAsync(collection, filter, cancellationToken: cancellationToken);
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
            var collections = await _client.ListCollectionsAsync(cancellationToken);
            return collections != null;
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

    private static object ConvertToPayloadValue(object value)
    {
        return value switch
        {
            string s => s,
            int i => i,
            long l => l,
            double d => d,
            float f => f,
            bool b => b,
            _ => value.ToString() ?? ""
        };
    }
}

