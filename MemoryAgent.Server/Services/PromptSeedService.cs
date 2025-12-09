using System.Text.Json;
using Neo4j.Driver;

namespace MemoryAgent.Server.Services;

/// <summary>
/// Seeds prompts from JSON file into Neo4j on startup
/// Prompts are stored in Data/Seeds/prompts.json
/// </summary>
public class PromptSeedService : IHostedService
{
    private readonly IDriver _neo4jDriver;
    private readonly ILogger<PromptSeedService> _logger;
    private readonly IConfiguration _configuration;

    public PromptSeedService(
        IDriver neo4jDriver,
        ILogger<PromptSeedService> logger,
        IConfiguration configuration)
    {
        _neo4jDriver = neo4jDriver;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // Only seed if enabled (default: true)
        var seedEnabled = _configuration.GetValue<bool>("Prompts:SeedOnStartup", true);
        if (!seedEnabled)
        {
            _logger.LogInformation("Prompt seeding disabled via configuration");
            return;
        }

        // Wait a bit for Neo4j to be ready
        await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);

        try
        {
            await SeedPromptsAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to seed prompts - will use hardcoded defaults");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task SeedPromptsAsync(CancellationToken cancellationToken)
    {
        // Find the seed file
        var seedPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "Seeds", "prompts.json");
        
        // Also check relative to working directory (for development)
        if (!File.Exists(seedPath))
        {
            seedPath = Path.Combine(Directory.GetCurrentDirectory(), "Data", "Seeds", "prompts.json");
        }

        if (!File.Exists(seedPath))
        {
            _logger.LogWarning("Prompt seed file not found at {Path} - skipping seed", seedPath);
            return;
        }

        _logger.LogInformation("Loading prompts from {Path}", seedPath);

        var json = await File.ReadAllTextAsync(seedPath, cancellationToken);
        var seedData = JsonSerializer.Deserialize<PromptSeedData>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (seedData?.Prompts == null || !seedData.Prompts.Any())
        {
            _logger.LogWarning("No prompts found in seed file");
            return;
        }

        _logger.LogInformation("Found {Count} prompts to seed (version {Version})", 
            seedData.Prompts.Count, seedData.Version);

        var seeded = 0;
        var skipped = 0;

        await using var session = _neo4jDriver.AsyncSession();

        foreach (var prompt in seedData.Prompts)
        {
            try
            {
                // Check if prompt already exists
                var checkResult = await session.ExecuteReadAsync(async tx =>
                {
                    var result = await tx.RunAsync(
                        "MATCH (p:Prompt {name: $name}) RETURN p.name as name, p.version as version",
                        new { name = prompt.Name });
                    return await result.ToListAsync();
                });

                if (checkResult.Any())
                {
                    var existingVersion = checkResult.First()["version"].As<int>();
                    _logger.LogDebug("Prompt '{Name}' already exists (v{Version}), skipping", 
                        prompt.Name, existingVersion);
                    skipped++;
                    continue;
                }

                // Create the prompt
                await session.ExecuteWriteAsync(async tx =>
                {
                    await tx.RunAsync(
                        @"CREATE (p:Prompt {
                            name: $name,
                            description: $description,
                            category: $category,
                            language: $language,
                            content: $content,
                            version: 1,
                            isActive: true,
                            totalUses: 0,
                            successfulUses: 0,
                            successRate: 0.5,
                            confidenceScore: 0.5,
                            createdAt: datetime(),
                            updatedAt: datetime()
                        })",
                        new
                        {
                            name = prompt.Name,
                            description = prompt.Description ?? "",
                            category = prompt.Category ?? "general",
                            language = prompt.Language ?? "",
                            content = prompt.Content
                        });
                });

                _logger.LogInformation("✅ Seeded prompt: {Name}", prompt.Name);
                seeded++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to seed prompt '{Name}'", prompt.Name);
            }
        }

        _logger.LogInformation("Prompt seeding complete: {Seeded} seeded, {Skipped} skipped", seeded, skipped);
    }

    private class PromptSeedData
    {
        public string? Version { get; set; }
        public string? Description { get; set; }
        public string? LastUpdated { get; set; }
        public List<PromptSeed>? Prompts { get; set; }
    }

    private class PromptSeed
    {
        public required string Name { get; set; }
        public string? Description { get; set; }
        public string? Category { get; set; }
        public string? Language { get; set; }
        public required string Content { get; set; }
    }

}

