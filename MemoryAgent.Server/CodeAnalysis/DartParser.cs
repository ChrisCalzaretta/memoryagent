using MemoryAgent.Server.Models;

namespace MemoryAgent.Server.CodeAnalysis;

/// <summary>
/// Parser for Dart/Flutter files - extracts code structure and patterns
/// </summary>
public class DartParser
{
    private readonly ILogger<DartParser> _logger;
    private readonly DartPatternDetector _dartDetector;
    private readonly FlutterPatternDetector _flutterDetector;

    public DartParser(ILogger<DartParser> logger)
    {
        _logger = logger;
        _dartDetector = new DartPatternDetector();
        _flutterDetector = new FlutterPatternDetector();
    }

    public async Task<ParseResult> ParseFileAsync(string filePath, string? context = null, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return new ParseResult { Errors = { $"File not found: {filePath}" } };
            }

            var code = await File.ReadAllTextAsync(filePath, cancellationToken);
            return await ParseCodeAsync(code, filePath, context, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing Dart file: {FilePath}", filePath);
            return new ParseResult { Errors = { $"Error parsing Dart file: {ex.Message}" } };
        }
    }

    public Task<ParseResult> ParseCodeAsync(string code, string filePath, string? context = null, CancellationToken cancellationToken = default)
    {
        var result = new ParseResult();

        try
        {
            _logger.LogInformation("🎯 Parsing Dart file: {FilePath}", filePath);

            // Determine context from file path if not provided
            if (string.IsNullOrWhiteSpace(context))
            {
                context = DetermineContext(filePath);
            }

            // Create file-level CodeMemory
            var fileInfo = new FileInfo(filePath);
            var fileMemory = new CodeMemory
            {
                Type = CodeMemoryType.File,
                Name = fileInfo.Name,
                Content = code,
                FilePath = filePath,
                Context = context,
                LineNumber = 0,
                Metadata = new Dictionary<string, object>
                {
                    ["language"] = "Dart",
                    ["file_size"] = code.Length,
                    ["line_count"] = code.Split('\n').Length
                }
            };
            result.CodeElements.Add(fileMemory);

            // Extract classes
            ExtractClasses(code, filePath, context, result);
            
            // Extract functions
            ExtractFunctions(code, filePath, context, result);
            
            // Extract imports
            ExtractImports(code, filePath, context, result);

            // Detect Dart patterns
            var dartPatterns = _dartDetector.DetectPatterns(code, filePath, context);
            _logger.LogDebug("Detected {Count} Dart patterns in {FilePath}", dartPatterns.Count, filePath);

            // Detect Flutter patterns
            var flutterPatterns = _flutterDetector.DetectPatterns(code, filePath, context);
            _logger.LogDebug("Detected {Count} Flutter patterns in {FilePath}", flutterPatterns.Count, filePath);

            // Store patterns in metadata for indexing
            var allPatterns = dartPatterns.Concat(flutterPatterns).ToList();
            if (allPatterns.Any() && result.CodeElements.Any())
            {
                result.CodeElements.First().Metadata["detected_patterns"] = allPatterns;
            }

            _logger.LogInformation("🎯 Dart parsing complete: {FilePath}, {Elements} elements, {Patterns} patterns",
                filePath, result.CodeElements.Count, allPatterns.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing Dart code: {FilePath}", filePath);
            result.Errors.Add($"Error parsing Dart code: {ex.Message}");
        }

        return Task.FromResult(result);
    }

    private void ExtractClasses(string code, string filePath, string context, ParseResult result)
    {
        // Match Dart class declarations
        var classPattern = @"(abstract\s+)?(class|mixin|extension)\s+(\w+)(\s+extends\s+(\w+))?(\s+with\s+([\w,\s]+))?(\s+implements\s+([\w,\s]+))?\s*\{";
        var matches = System.Text.RegularExpressions.Regex.Matches(code, classPattern);

        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            var isAbstract = !string.IsNullOrEmpty(match.Groups[1].Value);
            var classType = match.Groups[2].Value; // class, mixin, or extension
            var className = match.Groups[3].Value;
            var baseClass = match.Groups[5].Value;
            var mixins = match.Groups[7].Value;
            var interfaces = match.Groups[9].Value;

            var lineNumber = GetLineNumber(code, match.Index);
            var classBody = ExtractClassBody(code, match.Index);

            var classMemory = new CodeMemory
            {
                Type = CodeMemoryType.Class,
                Name = className,
                Content = classBody,
                FilePath = filePath,
                Context = context,
                LineNumber = lineNumber,
                Metadata = new Dictionary<string, object>
                {
                    ["language"] = "Dart",
                    ["class_type"] = classType,
                    ["is_abstract"] = isAbstract,
                    ["base_class"] = baseClass,
                    ["mixins"] = mixins.Split(',').Select(m => m.Trim()).Where(m => !string.IsNullOrEmpty(m)).ToList(),
                    ["interfaces"] = interfaces.Split(',').Select(i => i.Trim()).Where(i => !string.IsNullOrEmpty(i)).ToList()
                }
            };
            result.CodeElements.Add(classMemory);

            // Extract methods within class
            ExtractMethodsFromClass(classBody, className, filePath, context, lineNumber, result);
        }
    }

    private void ExtractFunctions(string code, string filePath, string context, ParseResult result)
    {
        // Match top-level function declarations (not in classes)
        var functionPattern = @"^\s*(Future<[\w<>,\s]+>|[\w<>,\s?]+)\s+(\w+)\s*\(([^)]*)\)\s*(async\s*)?\{";
        var matches = System.Text.RegularExpressions.Regex.Matches(code, functionPattern, 
            System.Text.RegularExpressions.RegexOptions.Multiline);

        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            var returnType = match.Groups[1].Value.Trim();
            var functionName = match.Groups[2].Value;
            var parameters = match.Groups[3].Value;
            var isAsync = !string.IsNullOrEmpty(match.Groups[4].Value);

            // Skip if it looks like a method (inside a class)
            if (IsInsideClass(code, match.Index))
                continue;

            var lineNumber = GetLineNumber(code, match.Index);

            var functionMemory = new CodeMemory
            {
                Type = CodeMemoryType.Method,
                Name = functionName,
                Content = ExtractFunctionBody(code, match.Index),
                FilePath = filePath,
                Context = context,
                LineNumber = lineNumber,
                Metadata = new Dictionary<string, object>
                {
                    ["language"] = "Dart",
                    ["return_type"] = returnType,
                    ["parameters"] = parameters,
                    ["is_async"] = isAsync,
                    ["is_top_level"] = true
                }
            };
            result.CodeElements.Add(functionMemory);
        }
    }

    private void ExtractMethodsFromClass(string classBody, string className, string filePath, string context, int classLineNumber, ParseResult result)
    {
        // Match method declarations
        var methodPattern = @"(static\s+)?(Future<[\w<>,\s]+>|[\w<>,\s?]+)\s+(\w+)\s*\(([^)]*)\)\s*(async\s*)?\{";
        var matches = System.Text.RegularExpressions.Regex.Matches(classBody, methodPattern);

        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            var isStatic = !string.IsNullOrEmpty(match.Groups[1].Value);
            var returnType = match.Groups[2].Value.Trim();
            var methodName = match.Groups[3].Value;
            var parameters = match.Groups[4].Value;
            var isAsync = !string.IsNullOrEmpty(match.Groups[5].Value);

            var lineNumber = classLineNumber + GetLineNumber(classBody, match.Index);

            var methodMemory = new CodeMemory
            {
                Type = CodeMemoryType.Method,
                Name = $"{className}.{methodName}",
                Content = ExtractFunctionBody(classBody, match.Index),
                FilePath = filePath,
                Context = context,
                LineNumber = lineNumber,
                Metadata = new Dictionary<string, object>
                {
                    ["language"] = "Dart",
                    ["class_name"] = className,
                    ["return_type"] = returnType,
                    ["parameters"] = parameters,
                    ["is_static"] = isStatic,
                    ["is_async"] = isAsync
                }
            };
            result.CodeElements.Add(methodMemory);
        }
    }

    private void ExtractImports(string code, string filePath, string context, ParseResult result)
    {
        // Match import statements
        var importPattern = @"import\s+['""]([^'""]+)['""](\s+as\s+(\w+))?(\s+show\s+([\w,\s]+))?(\s+hide\s+([\w,\s]+))?;";
        var matches = System.Text.RegularExpressions.Regex.Matches(code, importPattern);

        var imports = new List<Dictionary<string, object>>();
        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            imports.Add(new Dictionary<string, object>
            {
                ["path"] = match.Groups[1].Value,
                ["alias"] = match.Groups[3].Value,
                ["show"] = match.Groups[5].Value,
                ["hide"] = match.Groups[7].Value
            });
        }

        if (imports.Any() && result.CodeElements.Any())
        {
            result.CodeElements.First().Metadata["imports"] = imports;
        }
    }

    private string ExtractClassBody(string code, int startIndex)
    {
        var braceCount = 0;
        var started = false;
        var endIndex = startIndex;

        for (int i = startIndex; i < code.Length; i++)
        {
            if (code[i] == '{')
            {
                braceCount++;
                started = true;
            }
            else if (code[i] == '}')
            {
                braceCount--;
                if (started && braceCount == 0)
                {
                    endIndex = i + 1;
                    break;
                }
            }
        }

        return code.Substring(startIndex, endIndex - startIndex);
    }

    private string ExtractFunctionBody(string code, int startIndex)
    {
        var braceCount = 0;
        var started = false;
        var endIndex = startIndex;

        for (int i = startIndex; i < code.Length; i++)
        {
            if (code[i] == '{')
            {
                braceCount++;
                started = true;
            }
            else if (code[i] == '}')
            {
                braceCount--;
                if (started && braceCount == 0)
                {
                    endIndex = i + 1;
                    break;
                }
            }
        }

        return code.Substring(startIndex, Math.Min(endIndex - startIndex, 2000)); // Limit body size
    }

    private bool IsInsideClass(string code, int index)
    {
        // Simple check: count braces before this index
        var textBefore = code.Substring(0, index);
        var openBraces = textBefore.Count(c => c == '{');
        var closeBraces = textBefore.Count(c => c == '}');
        return openBraces > closeBraces;
    }

    private int GetLineNumber(string code, int charIndex)
    {
        if (charIndex < 0 || charIndex >= code.Length)
            return 1;

        return code.Substring(0, charIndex).Count(c => c == '\n') + 1;
    }

    private string DetermineContext(string filePath)
    {
        // Try to determine context from directory structure
        var parts = filePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        
        // Look for common project indicators
        for (int i = parts.Length - 2; i >= 0; i--)
        {
            var dir = parts[i].ToLowerInvariant();
            if (dir == "lib" || dir == "src" || dir == "app")
            {
                if (i > 0)
                    return parts[i - 1];
            }
            // Check for pubspec.yaml directory
            var dirPath = string.Join(Path.DirectorySeparatorChar.ToString(), parts.Take(i + 1));
            if (File.Exists(Path.Combine(dirPath, "pubspec.yaml")))
            {
                return parts[i];
            }
        }

        return "default";
    }
}

