using MemoryAgent.Server.Models;

namespace MemoryAgent.Server.CodeAnalysis;

/// <summary>
/// Composite parser that routes files to the appropriate language-specific parser
/// based on file extension. Uses production-quality AST parsers (NO REGEX!)
/// </summary>
public class CompositeCodeParser : ICodeParser
{
    private readonly RoslynParser _roslynParser;
    private readonly TypeScriptASTParser _tsParser;
    private readonly PythonASTParser _pythonParser;
    private readonly VBNetASTParser _vbParser;
    private readonly DartParser _dartParser;
    private readonly TerraformParser _terraformParser;
    private readonly BicepParser _bicepParser;
    private readonly ARMTemplateParser _armParser;
    private readonly JsonParser _jsonParser;
    private readonly ProjectFileParser _projectFileParser;
    private readonly ILogger<CompositeCodeParser> _logger;

    public CompositeCodeParser(
        RoslynParser roslynParser,
        TypeScriptASTParser tsParser,
        PythonASTParser pythonParser,
        VBNetASTParser vbParser,
        DartParser dartParser,
        TerraformParser terraformParser,
        BicepParser bicepParser,
        ARMTemplateParser armParser,
        JsonParser jsonParser,
        ProjectFileParser projectFileParser,
        ILogger<CompositeCodeParser> logger)
    {
        _roslynParser = roslynParser;
        _tsParser = tsParser;
        _pythonParser = pythonParser;
        _vbParser = vbParser;
        _dartParser = dartParser;
        _terraformParser = terraformParser;
        _bicepParser = bicepParser;
        _armParser = armParser;
        _jsonParser = jsonParser;
        _projectFileParser = projectFileParser;
        _logger = logger;
    }

    public async Task<ParseResult> ParseFileAsync(string filePath, string? context = null, CancellationToken cancellationToken = default)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        
        _logger.LogDebug("Routing {Extension} file to appropriate parser: {FilePath}", extension, filePath);

        return extension switch
        {
            // C# files - Roslyn AST
            ".cs" => await _roslynParser.ParseFileAsync(filePath, context, cancellationToken),
            
            // VB.NET files - Roslyn AST
            ".vb" => await _vbParser.ParseFileAsync(filePath, context, cancellationToken),
            
            // JavaScript/TypeScript/React/Node.js - TypeScript Compiler API
            ".js" or ".jsx" or ".ts" or ".tsx" or ".mjs" or ".cjs" => await _tsParser.ParseFileAsync(filePath, context, cancellationToken),
            
            // Python files - Python ast module via Python.NET
            ".py" => await _pythonParser.ParseFileAsync(filePath, context, cancellationToken),
            
            // Dart/Flutter files - Custom parser with pattern detection
            ".dart" => await _dartParser.ParseFileAsync(filePath, context, cancellationToken),
            
            // Terraform files - HCL parser with IaC pattern detection
            ".tf" or ".tfvars" => await _terraformParser.ParseFileAsync(filePath, context, cancellationToken),
            
            // Azure Bicep files - Bicep IaC parser
            ".bicep" => await _bicepParser.ParseFileAsync(filePath, context, cancellationToken),
            
            // JSON files - Try ARM template first, fall back to generic JSON chunking
            ".json" => await ParseJsonFileAsync(filePath, context, cancellationToken),
            
            // .NET Project/Solution files - MSBuild XML parsing
            ".sln" or ".csproj" or ".vbproj" or ".fsproj" => await Task.FromResult(
                _projectFileParser.ParseProjectFile(filePath, context)),
            
            // Unsupported types
            _ => CreateUnsupportedResult(filePath, extension, context)
        };
    }

    /// <summary>
    /// Parse JSON files: ARM templates get specialized parsing, generic JSON gets chunked
    /// </summary>
    private async Task<ParseResult> ParseJsonFileAsync(string filePath, string? context, CancellationToken cancellationToken)
    {
        // First, try ARM template parser (checks for $schema and resources)
        var armResult = await _armParser.ParseFileAsync(filePath, context, cancellationToken);
        
        // If ARM parser found something, use that result
        if (armResult.CodeElements.Count > 0)
        {
            _logger.LogDebug("Parsed {FilePath} as ARM template with {Count} elements", 
                filePath, armResult.CodeElements.Count);
            return armResult;
        }
        
        // Not an ARM template - use generic JSON parser
        _logger.LogDebug("File {FilePath} is not an ARM template, using generic JSON parser", filePath);
        return await _jsonParser.ParseFileAsync(filePath, context, cancellationToken);
    }

    public async Task<ParseResult> ParseCodeAsync(string code, string filePath, string? context = null, CancellationToken cancellationToken = default)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        
        _logger.LogDebug("Routing {Extension} code to appropriate parser: {FilePath}", extension, filePath);

        return extension switch
        {
            // C# code - Roslyn AST
            ".cs" => await _roslynParser.ParseCodeAsync(code, filePath, context, cancellationToken),
            
            // VB.NET code - Roslyn AST
            ".vb" => await _vbParser.ParseCodeAsync(code, filePath, context, cancellationToken),
            
            // JavaScript/TypeScript/React/Node.js - TypeScript Compiler API
            ".js" or ".jsx" or ".ts" or ".tsx" or ".mjs" or ".cjs" => await _tsParser.ParseCodeAsync(code, filePath, context, cancellationToken),
            
            // Python code - Python ast module via Python.NET
            ".py" => await _pythonParser.ParseCodeAsync(code, filePath, context, cancellationToken),
            
            // Dart/Flutter code - Custom parser with pattern detection
            ".dart" => await _dartParser.ParseCodeAsync(code, filePath, context, cancellationToken),
            
            // Terraform code - HCL parser with IaC pattern detection
            ".tf" or ".tfvars" => await _terraformParser.ParseCodeAsync(filePath, code, context, cancellationToken),
            
            // Azure Bicep code - Bicep IaC parser
            ".bicep" => await _bicepParser.ParseCodeAsync(code, filePath, context, cancellationToken),
            
            // JSON code - Try ARM template first, fall back to generic JSON
            ".json" => await ParseJsonCodeAsync(code, filePath, context, cancellationToken),
            
            // .NET Project/Solution files - write to temp and parse
            ".sln" or ".csproj" or ".vbproj" or ".fsproj" => await ParseProjectCodeAsync(code, filePath, extension, context, cancellationToken),
            
            // Unsupported types
            _ => CreateUnsupportedResult(filePath, extension, context)
        };
    }

    /// <summary>
    /// Parse .NET project/solution code: write to temp file and use ProjectFileParser
    /// </summary>
    private async Task<ParseResult> ParseProjectCodeAsync(string code, string? filePath, string extension, string? context, CancellationToken cancellationToken)
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"project-parse-{Guid.NewGuid():N}{extension}");
        try
        {
            await File.WriteAllTextAsync(tempFile, code, cancellationToken);
            var result = _projectFileParser.ParseProjectFile(tempFile, context);
            
            // Fix the file path in the result to use the original path
            foreach (var element in result.CodeElements)
            {
                element.FilePath = filePath ?? $"code{extension}";
            }
            
            return result;
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                try { File.Delete(tempFile); } catch { /* ignore cleanup errors */ }
            }
        }
    }

    /// <summary>
    /// Parse JSON code: ARM templates get specialized parsing, generic JSON gets chunked
    /// </summary>
    private async Task<ParseResult> ParseJsonCodeAsync(string code, string filePath, string? context, CancellationToken cancellationToken)
    {
        // First, try ARM template parser
        var armResult = await _armParser.ParseCodeAsync(code, filePath, context, cancellationToken);
        
        // If ARM parser found something, use that result
        if (armResult.CodeElements.Count > 0)
        {
            _logger.LogDebug("Parsed JSON code as ARM template with {Count} elements", armResult.CodeElements.Count);
            return armResult;
        }
        
        // Not an ARM template - for code parsing, create a temp file to use JsonParser
        // (JsonParser only has ParseFileAsync, so we write to temp)
        var tempFile = Path.Combine(Path.GetTempPath(), $"json-parse-{Guid.NewGuid():N}.json");
        try
        {
            await File.WriteAllTextAsync(tempFile, code, cancellationToken);
            var result = await _jsonParser.ParseFileAsync(tempFile, context, cancellationToken);
            
            // Fix the file path in the result to use the original path
            foreach (var element in result.CodeElements)
            {
                element.FilePath = filePath ?? "code.json";
            }
            
            return result;
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                try { File.Delete(tempFile); } catch { /* ignore cleanup errors */ }
            }
        }
    }

    private ParseResult CreateUnsupportedResult(string filePath, string extension, string? context)
    {
        _logger.LogWarning("Unsupported file type {Extension} for file: {FilePath}", extension, filePath);
        
        // Create a basic file-level entry for unsupported types (Markdown, JSON, etc.)
        // This allows them to be searchable even if we can't extract structure
        var result = new ParseResult();
        
        try
        {
            var content = File.ReadAllText(filePath);
            var fileNode = new CodeMemory
            {
                Type = CodeMemoryType.File,
                Name = Path.GetFileName(filePath),
                Content = content.Length > 5000 ? content.Substring(0, 5000) + "..." : content,
                FilePath = filePath,
                Context = context ?? "default",
                LineNumber = 1,
                Summary = $"File: {Path.GetFileName(filePath)}",
                Signature = extension,
                Tags = new List<string> { extension.TrimStart('.'), "unstructured" },
                Metadata = new Dictionary<string, object>
                {
                    ["file_type"] = extension,
                    ["parser"] = "basic",
                    ["is_structured"] = false
                }
            };
            
            result.CodeElements.Add(fileNode);
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Error reading file: {ex.Message}");
        }
        
        return result;
    }
}

