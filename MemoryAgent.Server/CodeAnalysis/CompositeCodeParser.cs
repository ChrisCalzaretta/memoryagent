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
    private readonly ILogger<CompositeCodeParser> _logger;

    public CompositeCodeParser(
        RoslynParser roslynParser,
        TypeScriptASTParser tsParser,
        PythonASTParser pythonParser,
        VBNetASTParser vbParser,
        DartParser dartParser,
        ILogger<CompositeCodeParser> logger)
    {
        _roslynParser = roslynParser;
        _tsParser = tsParser;
        _pythonParser = pythonParser;
        _vbParser = vbParser;
        _dartParser = dartParser;
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
            
            // Unsupported types
            _ => CreateUnsupportedResult(filePath, extension, context)
        };
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
            
            // Unsupported types
            _ => CreateUnsupportedResult(filePath, extension, context)
        };
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

