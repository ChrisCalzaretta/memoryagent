using System.Diagnostics;
using System.Text.Json;
using MemoryAgent.Server.Models;

namespace MemoryAgent.Server.CodeAnalysis;

/// <summary>
/// Parses JavaScript/TypeScript code using the TypeScript Compiler API via Node.js
/// Production-quality AST parser - NO REGEX!
/// Supports: .js, .jsx, .ts, .tsx, .mjs, .cjs, Node.js, React
/// </summary>
public class TypeScriptASTParser : ICodeParser
{
    private readonly ILogger<TypeScriptASTParser> _logger;
    private static readonly string ParserScriptPath = "/app/CodeAnalysis/ts-parser.js";

    public TypeScriptASTParser(ILogger<TypeScriptASTParser> logger)
    {
        _logger = logger;
    }

    public async Task<ParseResult> ParseFileAsync(string filePath, string? context = null, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return new ParseResult { Errors = { $"File not found: {filePath}" } };
            }

            // Determine context
            if (string.IsNullOrWhiteSpace(context))
            {
                context = DetermineContext(filePath);
            }

            // Call Node.js parser script
            var scriptPath = File.Exists(ParserScriptPath) 
                ? ParserScriptPath 
                : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CodeAnalysis", "ts-parser.js");

            var startInfo = new ProcessStartInfo
            {
                FileName = "node",
                Arguments = $"\"{scriptPath}\" \"{filePath}\" \"{context}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = startInfo };
            process.Start();

            var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

            await process.WaitForExitAsync(cancellationToken);

            var output = await outputTask;
            var errorOutput = await errorTask;

            if (process.ExitCode != 0)
            {
                _logger.LogError("TypeScript parser error for {FilePath}: {Error}", filePath, errorOutput);
                return new ParseResult { Errors = { $"TypeScript parser error: {errorOutput}" } };
            }

            if (string.IsNullOrWhiteSpace(output))
            {
                _logger.LogWarning("TypeScript parser returned empty output for {FilePath}", filePath);
                return new ParseResult { Errors = { "TypeScript parser returned empty output" } };
            }

            // Parse JSON output
            var jsonResult = JsonSerializer.Deserialize<TypeScriptParserResult>(output, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (jsonResult == null)
            {
                return new ParseResult { Errors = { "Failed to deserialize TypeScript parser output" } };
            }

            // Convert to ParseResult
            var parseResult = new ParseResult();

            // Add code elements
            foreach (var element in jsonResult.CodeElements ?? [])
            {
                parseResult.CodeElements.Add(new CodeMemory
                {
                    Type = Enum.Parse<CodeMemoryType>(element.Type, ignoreCase: true),
                    Name = element.Name,
                    Content = element.Content,
                    FilePath = element.FilePath,
                    Context = element.Context,
                    LineNumber = element.LineNumber,
                    Summary = element.Summary ?? string.Empty,
                    Signature = element.Signature ?? string.Empty,
                    Purpose = element.Purpose ?? string.Empty,
                    Tags = element.Tags?.ToList() ?? new List<string>(),
                    Dependencies = element.Dependencies?.ToList() ?? new List<string>(),
                    Metadata = element.Metadata ?? new Dictionary<string, object>()
                });
            }

            // Add relationships
            foreach (var rel in jsonResult.Relationships ?? [])
            {
                parseResult.Relationships.Add(new CodeRelationship
                {
                    FromName = rel.FromName,
                    ToName = rel.ToName,
                    Type = Enum.Parse<RelationshipType>(rel.Type, ignoreCase: true),
                    Context = rel.Context,
                    Properties = rel.Properties ?? new Dictionary<string, object>()
                });
            }

            // Add errors
            if (jsonResult.Errors != null && jsonResult.Errors.Any())
            {
                foreach (var error in jsonResult.Errors)
                {
                    parseResult.Errors.Add(error);
                }
            }

            return parseResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing TypeScript/JavaScript file: {FilePath}", filePath);
            return new ParseResult { Errors = { $"Error parsing TypeScript/JavaScript file: {ex.Message}" } };
        }
    }

    public async Task<ParseResult> ParseCodeAsync(string code, string filePath, string? context = null, CancellationToken cancellationToken = default)
    {
        // For in-memory parsing, write to temp file and parse
        try
        {
            var tempFile = Path.Combine(Path.GetTempPath(), $"temp_{Guid.NewGuid()}{Path.GetExtension(filePath)}");
            await File.WriteAllTextAsync(tempFile, code, cancellationToken);

            try
            {
                return await ParseFileAsync(tempFile, context, cancellationToken);
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing TypeScript/JavaScript code");
            return new ParseResult { Errors = { $"Error parsing TypeScript/JavaScript code: {ex.Message}" } };
        }
    }

    private string DetermineContext(string filePath)
    {
        var parts = filePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        
        // Look for common project root indicators
        for (int i = parts.Length - 1; i >= 0; i--)
        {
            if (parts[i] == "src" || parts[i] == "source" || parts[i] == "app" ||
                File.Exists(Path.Combine(string.Join(Path.DirectorySeparatorChar, parts.Take(i + 1)), "package.json")))
            {
                return i < parts.Length - 1 ? parts[i + 1] : parts[i];
            }
        }
        
        // Fallback to directory name containing the file
        return parts.Length >= 2 ? parts[^2] : "default";
    }

    // JSON models for deserialization
    private class TypeScriptParserResult
    {
        public List<TypeScriptCodeElement>? CodeElements { get; set; }
        public List<TypeScriptRelationship>? Relationships { get; set; }
        public List<string>? Errors { get; set; }
    }

    private class TypeScriptCodeElement
    {
        public string Type { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string Context { get; set; } = string.Empty;
        public int LineNumber { get; set; }
        public string? Summary { get; set; }
        public string? Signature { get; set; }
        public string? Purpose { get; set; }
        public List<string>? Tags { get; set; }
        public List<string>? Dependencies { get; set; }
        public Dictionary<string, object>? Metadata { get; set; }
    }

    private class TypeScriptRelationship
    {
        public string FromName { get; set; } = string.Empty;
        public string ToName { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Context { get; set; } = string.Empty;
        public Dictionary<string, object>? Properties { get; set; }
    }
}










