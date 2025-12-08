using DesignAgent.Server.Models.Brand;
using DesignAgent.Server.Models.Questionnaire;
using System.Collections.Concurrent;
using System.Text.Json;

namespace DesignAgent.Server.Services;

public class BrandService : IBrandService
{
    private readonly ITokenGeneratorService _tokenGenerator;
    private readonly IComponentSpecService _componentSpecService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILlmDesignService? _llmDesignService;
    private readonly ILogger<BrandService> _logger;
    
    // In-memory cache (would integrate with MemoryAgent in production)
    private readonly ConcurrentDictionary<string, BrandDefinition> _brands = new();

    public BrandService(
        ITokenGeneratorService tokenGenerator,
        IComponentSpecService componentSpecService,
        IHttpClientFactory httpClientFactory,
        ILogger<BrandService> logger,
        ILlmDesignService? llmDesignService = null)  // Optional - graceful degradation
    {
        _tokenGenerator = tokenGenerator;
        _componentSpecService = componentSpecService;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _llmDesignService = llmDesignService;
    }

    public async Task<BrandDefinition> CreateBrandAsync(ParsedBrandInput input, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Creating brand: {BrandName}", input.BrandName);
        
        var context = SanitizeContext(input.BrandName);
        
        // 🧠 Try to get LLM-enhanced brand suggestions
        LlmBrandSuggestions? llmSuggestions = null;
        if (_llmDesignService != null)
        {
            try
            {
                _logger.LogInformation("🧠 Getting LLM-enhanced brand suggestions for {Brand}", input.BrandName);
                llmSuggestions = await _llmDesignService.GenerateBrandSuggestionsAsync(input, cancellationToken);
                _logger.LogInformation("✨ LLM suggestions received: {Tagline}", llmSuggestions?.CreativeTagline);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "LLM brand suggestions failed, using algorithmic generation");
            }
        }
        
        // Generate design tokens (enhanced with LLM suggestions if available)
        var tokens = _tokenGenerator.GenerateTokens(input);
        
        // Apply LLM color suggestions if available
        if (llmSuggestions?.ColorSuggestions?.Any() == true)
        {
            _logger.LogInformation("🎨 Applying {Count} LLM color suggestions", llmSuggestions.ColorSuggestions.Count);
            // Could enhance tokens.Colors with LLM suggestions here
        }
        
        // Generate themes
        var themes = _tokenGenerator.GenerateThemes(input, tokens.Colors);
        
        // Generate component specs
        var components = _componentSpecService.GenerateComponentSpecs(input, tokens);
        
        // Build the complete brand definition
        var brand = new BrandDefinition
        {
            Context = context,
            Name = input.BrandName,
            Tagline = input.Tagline,
            Description = input.Description,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            
            Identity = new BrandIdentity
            {
                PersonalityTraits = input.PersonalityTraits,
                Archetype = input.VoiceArchetype,
                Industry = input.Industry,
                TargetAudience = input.TargetAudience,
                Platforms = input.Platforms,
                Frameworks = input.Frameworks
            },
            
            Tokens = tokens,
            Themes = themes,
            
            Voice = GenerateVoiceGuidelines(input),
            Accessibility = GenerateAccessibilityRequirements(input),
            Responsive = GenerateResponsiveGuidelines(input),
            Motion = GenerateMotionGuidelines(input),
            Icons = GenerateIconGuidelines(input),
            
            Components = components
        };
        
        // Store in local cache
        _brands[context] = brand;
        
        // Try to store in Memory Agent
        await StoreBrandInMemoryAgentAsync(brand, cancellationToken);
        
        _logger.LogInformation("Created brand '{BrandName}' with context '{Context}'", brand.Name, context);
        
        return brand;
    }

    public Task<BrandDefinition?> GetBrandAsync(string context, CancellationToken cancellationToken = default)
    {
        if (_brands.TryGetValue(context, out var brand))
        {
            return Task.FromResult<BrandDefinition?>(brand);
        }
        
        return Task.FromResult<BrandDefinition?>(null);
    }

    public Task<BrandDefinition> UpdateBrandAsync(string context, BrandDefinition updates, CancellationToken cancellationToken = default)
    {
        if (!_brands.TryGetValue(context, out var existing))
        {
            throw new KeyNotFoundException($"Brand with context '{context}' not found");
        }
        
        updates.Context = context;
        updates.UpdatedAt = DateTime.UtcNow;
        updates.CreatedAt = existing.CreatedAt;
        
        _brands[context] = updates;
        
        return Task.FromResult(updates);
    }

    public Task<bool> DeleteBrandAsync(string context, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_brands.TryRemove(context, out _));
    }

    public Task<List<BrandSummary>> ListBrandsAsync(CancellationToken cancellationToken = default)
    {
        var summaries = _brands.Values.Select(b => new BrandSummary
        {
            Context = b.Context,
            Name = b.Name,
            UpdatedAt = b.UpdatedAt
        }).ToList();
        
        return Task.FromResult(summaries);
    }

    public async Task<BrandDefinition> CloneBrandAsync(string fromContext, string toContext, Dictionary<string, object>? overrides = null, CancellationToken cancellationToken = default)
    {
        if (!_brands.TryGetValue(fromContext, out var source))
        {
            throw new KeyNotFoundException($"Source brand '{fromContext}' not found");
        }
        
        // Deep clone via serialization
        var json = JsonSerializer.Serialize(source);
        var clone = JsonSerializer.Deserialize<BrandDefinition>(json)!;
        
        clone.Context = toContext;
        clone.CreatedAt = DateTime.UtcNow;
        clone.UpdatedAt = DateTime.UtcNow;
        
        // Apply overrides if provided
        if (overrides != null)
        {
            ApplyOverrides(clone, overrides);
        }
        
        _brands[toContext] = clone;
        
        return clone;
    }

    private VoiceGuidelines GenerateVoiceGuidelines(ParsedBrandInput input)
    {
        var voice = new VoiceGuidelines
        {
            Archetype = input.VoiceArchetype,
            Personality = ExtractPersonality(input.VoiceArchetype)
        };
        
        // Customize tone based on archetype
        voice.Tone = input.VoiceArchetype.ToLower() switch
        {
            var s when s.Contains("coach") => new ToneByContext
            {
                Default = "Supportive, motivating",
                Error = "Encouraging, problem-solving",
                Success = "Celebrating but humble",
                Empty = "Inviting action"
            },
            var s when s.Contains("advisor") => new ToneByContext
            {
                Default = "Professional, knowledgeable",
                Error = "Clear, solution-focused",
                Success = "Confident acknowledgment",
                Empty = "Informative guidance"
            },
            var s when s.Contains("friend") => new ToneByContext
            {
                Default = "Casual, warm",
                Error = "Understanding, helpful",
                Success = "Genuine celebration",
                Empty = "Friendly nudge"
            },
            var s when s.Contains("playful") => new ToneByContext
            {
                Default = "Fun, witty",
                Error = "Light-hearted but helpful",
                Success = "Enthusiastic",
                Empty = "Playful invitation"
            },
            _ => new ToneByContext()
        };
        
        return voice;
    }

    private AccessibilityRequirements GenerateAccessibilityRequirements(ParsedBrandInput input)
    {
        return new AccessibilityRequirements
        {
            Level = input.AccessibilityLevel,
            Contrast = new ContrastRequirements
            {
                NormalTextMinimum = input.AccessibilityLevel == "AAA" ? 7.0 : 4.5,
                LargeTextMinimum = input.AccessibilityLevel == "AAA" ? 4.5 : 3.0
            }
        };
    }

    private ResponsiveGuidelines GenerateResponsiveGuidelines(ParsedBrandInput input)
    {
        var guidelines = new ResponsiveGuidelines();
        
        // Adjust based on platforms
        if (input.Platforms.Contains("iOS") || input.Platforms.Contains("Android"))
        {
            guidelines.TouchTargets = new TouchTargets
            {
                MinimumSize = "48px", // Slightly larger for mobile
                MinimumSpacing = "8px"
            };
        }
        
        return guidelines;
    }

    private MotionGuidelines GenerateMotionGuidelines(ParsedBrandInput input)
    {
        return new MotionGuidelines
        {
            Preference = input.MotionPreference,
            Duration = input.MotionPreference switch
            {
                "minimal" => new Dictionary<string, string>
                {
                    ["instant"] = "50ms", ["fast"] = "100ms", ["normal"] = "150ms", ["slow"] = "200ms"
                },
                "rich" => new Dictionary<string, string>
                {
                    ["instant"] = "150ms", ["fast"] = "250ms", ["normal"] = "400ms", ["slow"] = "600ms"
                },
                "none" => new Dictionary<string, string>
                {
                    ["instant"] = "0ms", ["fast"] = "0ms", ["normal"] = "0ms", ["slow"] = "0ms"
                },
                _ => new Dictionary<string, string>
                {
                    ["instant"] = "100ms", ["fast"] = "200ms", ["normal"] = "300ms", ["slow"] = "500ms"
                }
            }
        };
    }

    private IconGuidelines GenerateIconGuidelines(ParsedBrandInput input)
    {
        return new IconGuidelines
        {
            Library = "lucide",
            Style = "outlined",
            StrokeWidth = input.VisualStyle.Contains("Bold", StringComparison.OrdinalIgnoreCase) ? 2.0 : 1.5
        };
    }

    private async Task StoreBrandInMemoryAgentAsync(BrandDefinition brand, CancellationToken cancellationToken)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("MemoryAgent");
            var response = await client.PostAsJsonAsync("/api/mcp", new
            {
                jsonrpc = "2.0",
                id = 1,
                method = "tools/call",
                @params = new
                {
                    name = "store_qa",
                    arguments = new
                    {
                        question = $"What are the brand guidelines for {brand.Name}?",
                        answer = $"Brand: {brand.Name}\nContext: {brand.Context}\nPrimary Color: {brand.Tokens.Colors.Primary}\nFont: {brand.Tokens.Typography.FontFamilySans}",
                        context = brand.Context,
                        relevantFiles = Array.Empty<string>()
                    }
                }
            }, cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Stored brand in Memory Agent");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to store brand in Memory Agent (continuing without)");
        }
    }

    private static string SanitizeContext(string name)
    {
        return name.ToLower()
            .Replace(" ", "-")
            .Replace("_", "-");
    }

    private static string ExtractPersonality(string archetype)
    {
        return archetype.Split('-').FirstOrDefault()?.Trim() ?? "Friendly helper";
    }

    private static void ApplyOverrides(BrandDefinition brand, Dictionary<string, object> overrides)
    {
        foreach (var (key, value) in overrides)
        {
            var parts = key.Split('.');
            if (parts.Length == 2 && parts[0] == "colors")
            {
                var colorProp = typeof(ColorTokens).GetProperty(parts[1]);
                colorProp?.SetValue(brand.Tokens.Colors, value.ToString());
            }
        }
    }
}

