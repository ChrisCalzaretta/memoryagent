using System.Text;
using System.Text.RegularExpressions;
using MemoryAgent.Server.Models;

namespace MemoryAgent.Server.CodeAnalysis;

/// <summary>
/// Parser for Python files with smart chunking
/// </summary>
public class PythonParser
{
    public static ParseResult ParsePythonFile(string filePath, string? context = null)
    {
        var result = new ParseResult();
        
        try
        {
            if (!File.Exists(filePath))
            {
                result.Errors.Add($"File not found: {filePath}");
                return result;
            }

            var content = File.ReadAllText(filePath);
            var lines = content.Split('\n');
            var fileName = Path.GetFileName(filePath);
            
            // Create file-level node
            var fileNode = new CodeMemory
            {
                Type = CodeMemoryType.File,
                Name = fileName,
                Content = content.Length > 2000 ? content.Substring(0, 2000) + "..." : content,
                FilePath = filePath,
                Context = context ?? "default",
                LineNumber = 1,
                Metadata = new Dictionary<string, object>
                {
                    ["file_type"] = ".py",
                    ["is_python"] = true
                }
            };
            
            result.CodeElements.Add(fileNode);
            
            // Extract imports
            ExtractImports(lines, filePath, context, result);
            
            // Extract classes
            ExtractClasses(lines, filePath, context, result);
            
            // Extract functions (top-level)
            ExtractFunctions(lines, filePath, context, result);
            
            // Extract decorators
            ExtractDecorators(lines, filePath, context, result);
            
            // PATTERN DETECTION: Detect Python code patterns
            try
            {
                var patternDetector = new PythonPatternDetector();
                var detectedPatterns = patternDetector.DetectPatterns(content, filePath, context);
                
                if (detectedPatterns.Any())
                {
                    // Store patterns in result metadata for indexing service to process
                    if (!result.CodeElements.First().Metadata.ContainsKey("detected_patterns"))
                    {
                        result.CodeElements.First().Metadata["detected_patterns"] = detectedPatterns;
                    }
                }
            }
            catch (Exception patternEx)
            {
                // Don't fail the whole parse if pattern detection fails
                result.Errors.Add($"Warning: Failed to detect patterns: {patternEx.Message}");
            }
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Error parsing Python file: {ex.Message}");
        }
        
        return result;
    }
    
    private static void ExtractImports(string[] lines, string filePath, string? context, ParseResult result)
    {
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            
            // Match: import module
            // Match: from module import something
            if (line.StartsWith("import ") || line.StartsWith("from "))
            {
                var importMatch = Regex.Match(line, @"^(?:from\s+([^\s]+)\s+)?import\s+(.+)");
                if (importMatch.Success)
                {
                    var module = importMatch.Groups[1].Success ? importMatch.Groups[1].Value : importMatch.Groups[2].Value.Split(',')[0].Trim();
                    
                    // Create IMPORTS relationship
                    result.Relationships.Add(new CodeRelationship
                    {
                        FromName = Path.GetFileNameWithoutExtension(filePath),
                        ToName = module,
                        Type = RelationshipType.Imports,
                        Properties = new Dictionary<string, object>
                        {
                            ["line_number"] = i + 1,
                            ["import_statement"] = line
                        }
                    });
                }
            }
        }
    }
    
    private static void ExtractClasses(string[] lines, string filePath, string? context, ParseResult result)
    {
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var indent = GetIndentation(line);
            
            // Only top-level and first-level nested classes
            if (indent > 4) continue;
            
            var classMatch = Regex.Match(line.Trim(), @"^class\s+(\w+)(?:\(([^\)]*)\))?:");
            if (classMatch.Success)
            {
                var className = classMatch.Groups[1].Value;
                var baseClasses = classMatch.Groups[2].Success ? classMatch.Groups[2].Value : "";
                
                // Extract class body
                var classLines = new List<string> { line };
                var classIndent = indent;
                
                for (int j = i + 1; j < lines.Length; j++)
                {
                    var nextLine = lines[j];
                    var nextIndent = GetIndentation(nextLine);
                    
                    // Stop at same or lower indentation (unless empty line)
                    if (!string.IsNullOrWhiteSpace(nextLine) && nextIndent <= classIndent)
                        break;
                    
                    classLines.Add(nextLine);
                    
                    // Limit class size
                    if (classLines.Count > 200) break;
                }
                
                var classContent = string.Join("\n", classLines);
                
                var classNode = new CodeMemory
                {
                    Type = CodeMemoryType.Class,
                    Name = className,
                    Content = classContent,
                    FilePath = filePath,
                    Context = context ?? "default",
                    LineNumber = i + 1,
                    Metadata = new Dictionary<string, object>
                    {
                        ["language"] = "python",
                        ["base_classes"] = baseClasses
                    }
                };
                
                result.CodeElements.Add(classNode);
                
                // Create inheritance relationships
                if (!string.IsNullOrWhiteSpace(baseClasses))
                {
                    foreach (var baseClass in baseClasses.Split(','))
                    {
                        var trimmedBase = baseClass.Trim();
                        if (!string.IsNullOrWhiteSpace(trimmedBase))
                        {
                            result.Relationships.Add(new CodeRelationship
                            {
                                FromName = className,
                                ToName = trimmedBase,
                                Type = RelationshipType.Inherits,
                                Properties = new Dictionary<string, object>
                                {
                                    ["language"] = "python"
                                }
                            });
                        }
                    }
                }
                
                // Extract methods within this class
                ExtractMethods(classLines.ToArray(), filePath, context, className, i + 1, result);
            }
        }
    }
    
    private static void ExtractFunctions(string[] lines, string filePath, string? context, ParseResult result)
    {
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var indent = GetIndentation(line);
            
            // Only top-level functions
            if (indent > 0) continue;
            
            var funcMatch = Regex.Match(line.Trim(), @"^def\s+(\w+)\s*\(([^\)]*)\)(?:\s*->\s*([^:]+))?:");
            if (funcMatch.Success)
            {
                var funcName = funcMatch.Groups[1].Value;
                var parameters = funcMatch.Groups[2].Value;
                var returnType = funcMatch.Groups[3].Success ? funcMatch.Groups[3].Value.Trim() : null;
                
                // Extract function body
                var funcLines = new List<string> { line };
                
                for (int j = i + 1; j < lines.Length; j++)
                {
                    var nextLine = lines[j];
                    var nextIndent = GetIndentation(nextLine);
                    
                    if (!string.IsNullOrWhiteSpace(nextLine) && nextIndent <= indent)
                        break;
                    
                    funcLines.Add(nextLine);
                    
                    if (funcLines.Count > 100) break;
                }
                
                var funcContent = string.Join("\n", funcLines);
                
                var funcNode = new CodeMemory
                {
                    Type = CodeMemoryType.Method,
                    Name = funcName,
                    Content = funcContent,
                    FilePath = filePath,
                    Context = context ?? "default",
                    LineNumber = i + 1,
                    Metadata = new Dictionary<string, object>
                    {
                        ["language"] = "python",
                        ["parameters"] = parameters,
                        ["is_top_level"] = true
                    }
                };
                
                if (returnType != null)
                    funcNode.Metadata["return_type"] = returnType;
                
                result.CodeElements.Add(funcNode);
                
                // Create RETURNSTYPE relationship
                if (returnType != null)
                {
                    result.Relationships.Add(new CodeRelationship
                    {
                        FromName = funcName,
                        ToName = returnType,
                        Type = RelationshipType.ReturnsType,
                        Properties = new Dictionary<string, object>
                        {
                            ["language"] = "python"
                        }
                    });
                }
                
                // Extract function calls within
                ExtractFunctionCalls(funcLines, funcName, result);
            }
        }
    }
    
    private static void ExtractMethods(string[] classLines, string filePath, string? context, string className, int classStartLine, ParseResult result)
    {
        for (int i = 0; i < classLines.Length; i++)
        {
            var line = classLines[i];
            var indent = GetIndentation(line);
            
            // Methods should be indented one level inside class
            if (indent < 4) continue;
            
            var methodMatch = Regex.Match(line.Trim(), @"^def\s+(\w+)\s*\(([^\)]*)\)(?:\s*->\s*([^:]+))?:");
            if (methodMatch.Success)
            {
                var methodName = methodMatch.Groups[1].Value;
                var parameters = methodMatch.Groups[2].Value;
                var returnType = methodMatch.Groups[3].Success ? methodMatch.Groups[3].Value.Trim() : null;
                
                // Extract method body
                var methodLines = new List<string> { line };
                var methodIndent = indent;
                
                for (int j = i + 1; j < classLines.Length; j++)
                {
                    var nextLine = classLines[j];
                    var nextIndent = GetIndentation(nextLine);
                    
                    if (!string.IsNullOrWhiteSpace(nextLine) && nextIndent <= methodIndent)
                        break;
                    
                    methodLines.Add(nextLine);
                    
                    if (methodLines.Count > 100) break;
                }
                
                var methodContent = string.Join("\n", methodLines);
                var fullMethodName = $"{className}.{methodName}";
                
                // Calculate complexity metrics
                var cyclomaticComplexity = PythonComplexityAnalyzer.CalculateCyclomaticComplexity(methodLines);
                var cognitiveComplexity = PythonComplexityAnalyzer.CalculateCognitiveComplexity(methodLines);
                var linesOfCode = PythonComplexityAnalyzer.CalculateLinesOfCode(methodLines);
                var paramCount = parameters.Split(',').Length;
                var codeSmells = PythonComplexityAnalyzer.DetectCodeSmells(methodLines, paramCount);
                var exceptionTypes = PythonComplexityAnalyzer.ExtractExceptionTypes(methodLines);
                var dbCallCount = PythonComplexityAnalyzer.CountDatabaseCalls(methodLines);
                var hasHttpCalls = PythonComplexityAnalyzer.HasHttpCalls(methodLines);
                var hasLogging = PythonComplexityAnalyzer.HasLogging(methodLines);
                var isAsync = PythonComplexityAnalyzer.IsAsync(methodLines);
                
                // Check if this is a test method
                var decorators = classLines.Take(i).TakeLast(5)
                    .Where(l => l.Trim().StartsWith("@"))
                    .Select(l => l.Trim())
                    .ToList();
                var isTestMethod = PythonComplexityAnalyzer.IsTestMethod(methodName, decorators);
                
                var methodNode = new CodeMemory
                {
                    Type = isTestMethod ? CodeMemoryType.Test : CodeMemoryType.Method,
                    Name = fullMethodName,
                    Content = methodContent,
                    FilePath = filePath,
                    Context = context ?? "default",
                    LineNumber = classStartLine + i,
                    Metadata = new Dictionary<string, object>
                    {
                        ["language"] = "python",
                        ["class_name"] = className,
                        ["method_name"] = methodName,
                        ["parameters"] = parameters,
                        ["parameter_count"] = paramCount,
                        ["is_async"] = isAsync,
                        
                        // Code quality metrics
                        ["cyclomatic_complexity"] = cyclomaticComplexity,
                        ["cognitive_complexity"] = cognitiveComplexity,
                        ["lines_of_code"] = linesOfCode,
                        ["code_smells"] = codeSmells,
                        ["code_smell_count"] = codeSmells.Count,
                        
                        // Exception handling
                        ["exception_types"] = exceptionTypes,
                        ["throws_exceptions"] = exceptionTypes.Any(),
                        
                        // External dependencies
                        ["database_calls"] = dbCallCount,
                        ["has_database_access"] = dbCallCount > 0,
                        ["has_http_calls"] = hasHttpCalls,
                        ["has_logging"] = hasLogging,
                        
                        // Test metadata
                        ["is_test"] = isTestMethod
                    }
                };
                
                if (returnType != null)
                    methodNode.Metadata["return_type"] = returnType;
                
                // Add test-specific metadata
                if (isTestMethod)
                {
                    var fileContent = File.ReadAllText(filePath);
                    var imports = fileContent.Split('\n').Where(l => l.StartsWith("import ") || l.StartsWith("from ")).ToList();
                    methodNode.Metadata["test_framework"] = PythonComplexityAnalyzer.DetectTestFramework(decorators, imports);
                    methodNode.Metadata["assertion_count"] = PythonComplexityAnalyzer.CountAssertions(methodLines);
                }
                
                result.CodeElements.Add(methodNode);
                
                // Create DEFINES relationship (class defines method)
                result.Relationships.Add(new CodeRelationship
                {
                    FromName = className,
                    ToName = fullMethodName,
                    Type = RelationshipType.Defines
                });
                
                // Extract function calls
                ExtractFunctionCalls(methodLines, fullMethodName, result);
            }
        }
    }
    
    private static void ExtractFunctionCalls(List<string> lines, string callerName, ParseResult result)
    {
        foreach (var line in lines)
        {
            // Match function calls: function_name(
            var callMatches = Regex.Matches(line, @"(\w+)\s*\(");
            foreach (Match match in callMatches)
            {
                var calledFunc = match.Groups[1].Value;
                
                // Skip common built-ins and keywords
                if (new[] { "if", "for", "while", "def", "class", "return", "print" }.Contains(calledFunc))
                    continue;
                
                result.Relationships.Add(new CodeRelationship
                {
                    FromName = callerName,
                    ToName = calledFunc,
                    Type = RelationshipType.Calls,
                    Properties = new Dictionary<string, object>
                    {
                        ["language"] = "python"
                    }
                });
            }
        }
    }
    
    private static void ExtractDecorators(string[] lines, string filePath, string? context, ParseResult result)
    {
        for (int i = 0; i < lines.Length - 1; i++)
        {
            var line = lines[i].Trim();
            
            if (line.StartsWith("@"))
            {
                var decoratorName = line.TrimStart('@').Split('(')[0].Trim();
                
                // Check next line for function/method definition
                var nextLine = lines[i + 1].Trim();
                var funcMatch = Regex.Match(nextLine, @"def\s+(\w+)");
                
                if (funcMatch.Success)
                {
                    var funcName = funcMatch.Groups[1].Value;
                    
                    result.Relationships.Add(new CodeRelationship
                    {
                        FromName = funcName,
                        ToName = decoratorName,
                        Type = RelationshipType.HasAttribute,
                        Properties = new Dictionary<string, object>
                        {
                            ["language"] = "python",
                            ["decorator"] = decoratorName
                        }
                    });
                }
            }
        }
    }
    
    private static int GetIndentation(string line)
    {
        int count = 0;
        foreach (char c in line)
        {
            if (c == ' ') count++;
            else if (c == '\t') count += 4;
            else break;
        }
        return count;
    }
}

