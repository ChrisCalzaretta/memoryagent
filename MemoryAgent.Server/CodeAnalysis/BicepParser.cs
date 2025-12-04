using MemoryAgent.Server.Models;
using Microsoft.Extensions.Logging;

namespace MemoryAgent.Server.CodeAnalysis;

/// <summary>
/// Parser for Azure Bicep (.bicep) Infrastructure as Code files
/// Reference: https://learn.microsoft.com/en-us/azure/azure-resource-manager/bicep/
/// </summary>
public class BicepParser : ICodeParser
{
    private readonly BicepPatternDetector _patternDetector;
    private readonly ILogger<BicepParser> _logger;

    public BicepParser(ILogger<BicepParser> logger)
    {
        _logger = logger;
        _patternDetector = new BicepPatternDetector();
    }

    public async Task<ParseResult> ParseFileAsync(
        string filePath,
        string? context = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Parsing Bicep file: {FilePath}", filePath);

            if (!File.Exists(filePath))
            {
                return new ParseResult
                {
                    Errors = new List<string> { $"File not found: {filePath}" },
                };
            }

            var sourceCode = await File.ReadAllTextAsync(filePath, cancellationToken);
            return await ParseCodeAsync(sourceCode, filePath, context, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing Bicep file: {FilePath}", filePath);
            return new ParseResult
            {
                Errors = new List<string> { $"Parse error: {ex.Message}" },
            };
        }
    }

    public async Task<ParseResult> ParseCodeAsync(
        string sourceCode,
        string? filePath = null,
        string? context = null,
        CancellationToken cancellationToken = default)
    {
        var result = new ParseResult();

        try
        {
            var patterns = await _patternDetector.DetectPatternsAsync(
                filePath ?? "bicep_code",
                context,
                sourceCode,
                cancellationToken);

            // Create a single CodeMemory representing the Bicep file
            var element = new CodeMemory
            {
                Name = Path.GetFileName(filePath ?? "bicep_file.bicep"),
                Type = CodeMemoryType.File,
                FilePath = filePath ?? "bicep_file.bicep",
                Content = sourceCode,
                Context = context ?? "",
                LineNumber = 1,
                Summary = $"Bicep Infrastructure as Code file with {patterns.Count} patterns detected",
                Purpose = "Azure infrastructure definition using Bicep declarative syntax",
                Tags = new List<string> { "Bicep", "IaC", "Azure", "Infrastructure" },
                Metadata = new Dictionary<string, object>
                {
                    ["language"] = "Bicep",
                    ["pattern_count"] = patterns.Count,
                    ["file_type"] = "IaC",
                    ["resource_count"] = patterns.Count(p => p.Name == "Bicep_Resource"),
                    ["module_count"] = patterns.Count(p => p.Name == "Bicep_Module"),
                    ["parameter_count"] = patterns.Count(p => p.Name == "Bicep_Parameter"),
                    ["output_count"] = patterns.Count(p => p.Name == "Bicep_Output"),
                    ["has_decorators"] = patterns.Any(p => p.Name.StartsWith("Bicep_Decorator_")),
                    ["has_loops"] = patterns.Any(p => p.Name == "Bicep_Loop"),
                    ["has_conditionals"] = patterns.Any(p => p.Name == "Bicep_ConditionalDeployment"),
                    ["anti_pattern_count"] = patterns.Count(p => p.Name.Contains("AntiPattern")),
                    ["patterns"] = patterns  // Store patterns in metadata
                }
            };

            result.CodeElements.Add(element);

            _logger.LogInformation(
                "Bicep parse complete: {ElementCount} elements, {PatternCount} patterns",
                result.CodeElements.Count,
                patterns.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Bicep ParseCodeAsync");
            result.Errors.Add($"Parse error: {ex.Message}");
        }

        return result;
    }
}
