using MemoryAgent.Server.Models;
using Microsoft.Extensions.Logging;

namespace MemoryAgent.Server.CodeAnalysis;

/// <summary>
/// Parser for Azure ARM Template (.json) Infrastructure as Code files
/// Handles: mainTemplate.json, azuredeploy.json, createUiDefinition.json, parameter files
/// Reference: https://learn.microsoft.com/en-us/azure/azure-resource-manager/templates/syntax
/// </summary>
public class ARMTemplateParser : ICodeParser
{
    private readonly ARMTemplatePatternDetector _patternDetector;
    private readonly ILogger<ARMTemplateParser> _logger;

    public ARMTemplateParser(ILogger<ARMTemplateParser> logger)
    {
        _logger = logger;
        _patternDetector = new ARMTemplatePatternDetector();
    }

    public async Task<ParseResult> ParseFileAsync(
        string filePath,
        string? context = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Parsing ARM template file: {FilePath}", filePath);

            if (!File.Exists(filePath))
            {
            return new ParseResult
            {
                Errors = new List<string> { $"File not found: {filePath}" }
            };
            }

            // Only parse if it looks like an ARM template (has $schema or resources)
            var sourceCode = await File.ReadAllTextAsync(filePath, cancellationToken);
            
            if (!IsARMTemplate(sourceCode))
            {
                _logger.LogDebug("File {FilePath} is not an ARM template, skipping", filePath);
                return new ParseResult();
            }

            return await ParseCodeAsync(sourceCode, filePath, context, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing ARM template file: {FilePath}", filePath);
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
                filePath ?? "arm_template.json",
                context,
                sourceCode,
                cancellationToken);

            // Determine template type
            var templateType = DetermineTemplateType(filePath, sourceCode);

            // Create a single CodeMemory representing the ARM template
            var element = new CodeMemory
            {
                Name = Path.GetFileName(filePath ?? "arm_template.json"),
                Type = CodeMemoryType.File,
                FilePath = filePath ?? "arm_template.json",
                Content = sourceCode,
                Context = context ?? "",
                LineNumber = 1,
                Summary = $"ARM Template ({templateType}) with {patterns.Count} patterns detected",
                Purpose = "Azure infrastructure deployment using ARM template JSON syntax",
                Tags = new List<string> { "ARM", "IaC", "Azure", "Infrastructure", "JSON", templateType },
                Metadata = new Dictionary<string, object>
                {
                    ["language"] = "JSON",
                    ["template_type"] = templateType,
                    ["pattern_count"] = patterns.Count,
                    ["file_type"] = "IaC",
                    ["resource_count"] = patterns.Count(p => p.Name == "ARM_Resource"),
                    ["parameter_count"] = patterns.Count(p => p.Name == "ARM_Parameter"),
                    ["output_count"] = patterns.Count(p => p.Name == "ARM_Output"),
                    ["has_variables"] = patterns.Any(p => p.Name == "ARM_Variables"),
                    ["has_functions"] = patterns.Any(p => p.Name == "ARM_UserDefinedFunctions"),
                    ["has_copy_loops"] = patterns.Any(p => p.Name == "ARM_CopyLoop"),
                    ["has_linked_templates"] = patterns.Any(p => p.Name == "ARM_LinkedTemplate"),
                    ["is_parameter_file"] = patterns.Any(p => p.Name == "ARM_ParameterFile"),
                    ["anti_pattern_count"] = patterns.Count(p => p.Name.Contains("AntiPattern")),
                    ["patterns"] = patterns  // Store patterns in metadata
                }
            };

            result.CodeElements.Add(element);

            _logger.LogInformation(
                "ARM template parse complete: {ElementCount} elements, {PatternCount} patterns",
                result.CodeElements.Count,
                patterns.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ARM template ParseCodeAsync");
            result.Errors.Add($"Parse error: {ex.Message}");
        }

        return result;
    }

    private bool IsARMTemplate(string sourceCode)
    {
        // Check for ARM template indicators
        return sourceCode.Contains("$schema") &&
               (sourceCode.Contains("deploymentTemplate.json") ||
                sourceCode.Contains("managedapplication") ||
                sourceCode.Contains("\"resources\"") ||
                sourceCode.Contains("\"parameters\""));
    }

    private string DetermineTemplateType(string? filePath, string sourceCode)
    {
        if (filePath != null)
        {
            var fileName = Path.GetFileName(filePath).ToLowerInvariant();
            
            if (fileName.Contains("createuidefinition"))
                return "CreateUiDefinition";
            if (fileName.Contains("viewdefinition"))
                return "ViewDefinition";
            if (fileName.Contains("parameters") || fileName.Contains(".parameters."))
                return "ParameterFile";
            if (fileName.Contains("maintemplate") || fileName.Contains("azuredeploy"))
                return "MainTemplate";
        }

        // Check content
        if (sourceCode.Contains("\"resources\"") && sourceCode.Count(c => c == '{') > 20)
            return "MainTemplate";
        if (sourceCode.Contains("\"parameters\"") && !sourceCode.Contains("\"resources\""))
            return "ParameterFile";

        return "ARMTemplate";
    }
}

