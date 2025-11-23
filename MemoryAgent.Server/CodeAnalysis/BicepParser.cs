using MemoryAgent.Server.Models;
using System.Text.RegularExpressions;

namespace MemoryAgent.Server.CodeAnalysis;

/// <summary>
/// Parser for Azure Bicep files
/// </summary>
public class BicepParser
{
    private readonly ILogger<BicepParser> _logger;

    public BicepParser(ILogger<BicepParser> logger)
    {
        _logger = logger;
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

            var content = await File.ReadAllTextAsync(filePath, cancellationToken);
            
            // Extract resources
            ExtractResources(content, filePath, context, result);
            
            // Extract modules
            ExtractModules(content, filePath, context, result);
            
            // Extract parameters
            ExtractParameters(content, filePath, context, result);
            
            // Extract variables
            ExtractVariables(content, filePath, context, result);
            
            // Extract outputs
            ExtractOutputs(content, filePath, context, result);

            _logger.LogDebug("Parsed Bicep file {FilePath}: {ResourceCount} resources, {ModuleCount} modules", 
                filePath, result.CodeElements.Count(e => e.Metadata.ContainsKey("bicep_type") && e.Metadata["bicep_type"].ToString() == "resource"),
                result.CodeElements.Count(e => e.Metadata.ContainsKey("bicep_type") && e.Metadata["bicep_type"].ToString() == "module"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing Bicep file: {FilePath}", filePath);
            result.Errors.Add($"Error parsing file: {ex.Message}");
        }

        return result;
    }

    private void ExtractResources(string content, string filePath, string? context, ParseResult result)
    {
        // Match: resource <name> '<type>@<version>' = {
        var resourceRegex = new Regex(@"resource\s+(\w+)\s+'([^']+)'\s*=\s*\{([^}]*(?:\{[^}]*\}[^}]*)*)\}", RegexOptions.Singleline);
        
        foreach (Match match in resourceRegex.Matches(content))
        {
            var resourceName = match.Groups[1].Value;
            var resourceType = match.Groups[2].Value;
            var resourceBody = match.Groups[3].Value;
            var lineNumber = content.Substring(0, match.Index).Count(c => c == '\n') + 1;

            var codeMemory = new CodeMemory
            {
                Type = CodeMemoryType.Pattern,
                Name = resourceName,
                Content = $"resource {resourceName} '{resourceType}' = {{{resourceBody}}}",
                FilePath = filePath,
                Context = context ?? "default",
                LineNumber = lineNumber,
                Metadata = new Dictionary<string, object>
                {
                    ["bicep_type"] = "resource",
                    ["resource_type"] = resourceType,
                    ["resource_name"] = resourceName
                }
            };

            result.CodeElements.Add(codeMemory);
            result.Relationships.Add(new CodeRelationship
            {
                FromName = Path.GetFileName(filePath),
                ToName = resourceName,
                Type = RelationshipType.Defines,
                Context = context ?? "default"
            });
        }
    }

    private void ExtractModules(string content, string filePath, string? context, ParseResult result)
    {
        // Match: module <name> '<path>' = {
        var moduleRegex = new Regex(@"module\s+(\w+)\s+'([^']+)'\s*=\s*\{([^}]*(?:\{[^}]*\}[^}]*)*)\}", RegexOptions.Singleline);
        
        foreach (Match match in moduleRegex.Matches(content))
        {
            var moduleName = match.Groups[1].Value;
            var modulePath = match.Groups[2].Value;
            var moduleBody = match.Groups[3].Value;
            var lineNumber = content.Substring(0, match.Index).Count(c => c == '\n') + 1;

            var codeMemory = new CodeMemory
            {
                Type = CodeMemoryType.Pattern,
                Name = moduleName,
                Content = $"module {moduleName} '{modulePath}' = {{{moduleBody}}}",
                FilePath = filePath,
                Context = context ?? "default",
                LineNumber = lineNumber,
                Metadata = new Dictionary<string, object>
                {
                    ["bicep_type"] = "module",
                    ["module_path"] = modulePath,
                    ["module_name"] = moduleName
                }
            };

            result.CodeElements.Add(codeMemory);
            result.Relationships.Add(new CodeRelationship
            {
                FromName = Path.GetFileName(filePath),
                ToName = moduleName,
                Type = RelationshipType.Uses,
                Context = context ?? "default"
            });
        }
    }

    private void ExtractParameters(string content, string filePath, string? context, ParseResult result)
    {
        // Match: param <name> <type> = <defaultValue>
        var paramRegex = new Regex(@"param\s+(\w+)\s+(\w+)(?:\s*=\s*(.+?))?(?=\r?\n|$)", RegexOptions.Multiline);
        
        foreach (Match match in paramRegex.Matches(content))
        {
            var paramName = match.Groups[1].Value;
            var paramType = match.Groups[2].Value;
            var defaultValue = match.Groups[3].Value;
            var lineNumber = content.Substring(0, match.Index).Count(c => c == '\n') + 1;

            var codeMemory = new CodeMemory
            {
                Type = CodeMemoryType.Pattern,
                Name = paramName,
                Content = match.Value.Trim(),
                FilePath = filePath,
                Context = context ?? "default",
                LineNumber = lineNumber,
                Metadata = new Dictionary<string, object>
                {
                    ["bicep_type"] = "parameter",
                    ["param_type"] = paramType,
                    ["has_default"] = !string.IsNullOrWhiteSpace(defaultValue)
                }
            };

            result.CodeElements.Add(codeMemory);
        }
    }

    private void ExtractVariables(string content, string filePath, string? context, ParseResult result)
    {
        // Match: var <name> = <value>
        var varRegex = new Regex(@"var\s+(\w+)\s*=\s*(.+?)(?=\r?\n|$)", RegexOptions.Multiline);
        
        foreach (Match match in varRegex.Matches(content))
        {
            var varName = match.Groups[1].Value;
            var varValue = match.Groups[2].Value;
            var lineNumber = content.Substring(0, match.Index).Count(c => c == '\n') + 1;

            var codeMemory = new CodeMemory
            {
                Type = CodeMemoryType.Pattern,
                Name = varName,
                Content = match.Value.Trim(),
                FilePath = filePath,
                Context = context ?? "default",
                LineNumber = lineNumber,
                Metadata = new Dictionary<string, object>
                {
                    ["bicep_type"] = "variable",
                    ["var_name"] = varName
                }
            };

            result.CodeElements.Add(codeMemory);
        }
    }

    private void ExtractOutputs(string content, string filePath, string? context, ParseResult result)
    {
        // Match: output <name> <type> = <value>
        var outputRegex = new Regex(@"output\s+(\w+)\s+(\w+)\s*=\s*(.+?)(?=\r?\n|$)", RegexOptions.Multiline);
        
        foreach (Match match in outputRegex.Matches(content))
        {
            var outputName = match.Groups[1].Value;
            var outputType = match.Groups[2].Value;
            var outputValue = match.Groups[3].Value;
            var lineNumber = content.Substring(0, match.Index).Count(c => c == '\n') + 1;

            var codeMemory = new CodeMemory
            {
                Type = CodeMemoryType.Pattern,
                Name = outputName,
                Content = match.Value.Trim(),
                FilePath = filePath,
                Context = context ?? "default",
                LineNumber = lineNumber,
                Metadata = new Dictionary<string, object>
                {
                    ["bicep_type"] = "output",
                    ["output_type"] = outputType,
                    ["output_name"] = outputName
                }
            };

            result.CodeElements.Add(codeMemory);
        }
    }
}

