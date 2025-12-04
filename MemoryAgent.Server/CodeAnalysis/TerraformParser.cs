using MemoryAgent.Server.Models;
using Microsoft.Extensions.Logging;

namespace MemoryAgent.Server.CodeAnalysis;

/// <summary>
/// Parser for Terraform (.tf) files
/// Simplified parser that focuses on pattern detection
/// </summary>
public class TerraformParser : ICodeParser
{
    private readonly TerraformPatternDetector _patternDetector;
    private readonly ILogger<TerraformParser> _logger;

    public TerraformParser(ILogger<TerraformParser> logger)
    {
        _logger = logger;
        _patternDetector = new TerraformPatternDetector();
    }

    public async Task<ParseResult> ParseFileAsync(string filePath, string? context = null, CancellationToken cancellationToken = default)
    {
        var result = new ParseResult();

        try
        {
            if (!File.Exists(filePath))
            {
                result.Errors.Add($"File not found: {filePath}");
                return result;
            }

            var sourceCode = await File.ReadAllTextAsync(filePath, cancellationToken);
            return await ParseCodeAsync(filePath, sourceCode, context, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing Terraform file: {FilePath}", filePath);
            result.Errors.Add($"Parse error: {ex.Message}");
        }

        return result;
    }

    public async Task<ParseResult> ParseCodeAsync(string filePath, string sourceCode, string? context = null, CancellationToken cancellationToken = default)
    {
        var result = new ParseResult();

        try
        {
            // Detect Terraform patterns - patterns contain all metadata we need
            var patterns = await _patternDetector.DetectPatternsAsync(filePath, context, sourceCode, cancellationToken);

            _logger.LogInformation(
                "Parsed Terraform file {FilePath}: {PatternCount} patterns detected",
                filePath,
                patterns.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing Terraform code: {FilePath}", filePath);
            result.Errors.Add($"Parse error: {ex.Message}");
        }

        return result;
    }
}
