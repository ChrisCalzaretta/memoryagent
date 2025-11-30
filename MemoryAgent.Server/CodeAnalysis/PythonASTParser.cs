using System.Text;
using System.Text.Json;
using MemoryAgent.Server.Models;
using Python.Runtime;

namespace MemoryAgent.Server.CodeAnalysis;

/// <summary>
/// Parses Python code using Python.NET and the built-in ast module
/// Production-quality AST parser - NO REGEX!
/// </summary>
public class PythonASTParser : ICodeParser
{
    private readonly ILogger<PythonASTParser> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private static bool _pythonInitialized = false;
    private static readonly object _lock = new object();

    public PythonASTParser(ILogger<PythonASTParser> logger, ILoggerFactory loggerFactory)
    {
        _logger = logger;
        _loggerFactory = loggerFactory;
        InitializePython();
    }

    private void InitializePython()
    {
        lock (_lock)
        {
            if (!_pythonInitialized)
            {
                try
                {
                    // Set Python home if needed (Docker will have python3 in PATH)
                    if (string.IsNullOrEmpty(Runtime.PythonDLL))
                    {
                        // Try to find Python in common locations
                        var possiblePaths = new[]
                        {
                            "/usr/lib/libpython3.so",
                            "/usr/lib/x86_64-linux-gnu/libpython3.11.so",
                            "/usr/lib/x86_64-linux-gnu/libpython3.10.so",
                            "/usr/lib/x86_64-linux-gnu/libpython3.9.so",
                            "python311.dll", // Windows
                            "python310.dll",
                            "python39.dll"
                        };

                        foreach (var path in possiblePaths)
                        {
                            if (File.Exists(path))
                            {
                                Runtime.PythonDLL = path;
                                _logger.LogInformation("🐍 Found Python DLL: {Path}", path);
                                break;
                            }
                        }
                    }

                    _logger.LogInformation("🐍 Initializing PythonEngine...");
                    PythonEngine.Initialize();
                    
                    // CRITICAL FIX: Allow threads after initialization
                    // This releases the GIL so other threads can acquire it
                    _logger.LogInformation("🐍 Enabling Python threading support...");
                    PythonEngine.BeginAllowThreads();
                    
                    _pythonInitialized = true;
                    _logger.LogInformation("🐍 Python.NET initialized successfully with threading support!");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "🐍 FATAL: Failed to initialize Python.NET");
                    throw;
                }
            }
        }
    }

    public async Task<ParseResult> ParseFileAsync(string filePath, string? context = null, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("🐍 PythonASTParser.ParseFileAsync START: {FilePath}", filePath);
            
            if (!File.Exists(filePath))
            {
                _logger.LogWarning("🐍 PythonASTParser: File not found: {FilePath}", filePath);
                return new ParseResult { Errors = { $"File not found: {filePath}" } };
            }

            _logger.LogInformation("🐍 PythonASTParser: Reading file content...");
            var code = await File.ReadAllTextAsync(filePath, cancellationToken);
            
            _logger.LogInformation("🐍 PythonASTParser: Calling ParseCodeAsync...");
            var result = await ParseCodeAsync(code, filePath, context, cancellationToken);
            
            _logger.LogInformation("🐍 PythonASTParser.ParseFileAsync COMPLETE: {FilePath}, Elements: {Count}, Success: {Success}", 
                filePath, result.CodeElements.Count, result.Success);
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "🐍 PythonASTParser ERROR in ParseFileAsync: {FilePath}", filePath);
            return new ParseResult { Errors = { $"Error parsing Python file: {ex.Message}" } };
        }
    }

    public async Task<ParseResult> ParseCodeAsync(string code, string filePath, string? context = null, CancellationToken cancellationToken = default)
    {
        var result = new ParseResult();

        try
        {
            _logger.LogInformation("🐍 ParseCodeAsync START: {FilePath}, Code length: {Length}", filePath, code.Length);
            
            // Determine context
            if (string.IsNullOrWhiteSpace(context))
            {
                context = DetermineContext(filePath);
            }

            _logger.LogInformation("🐍 Creating file-level CodeMemory for context: {Context}", context);
            
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
                    ["file_size"] = fileInfo.Length,
                    ["language"] = "python"
                }
            };
            result.CodeElements.Add(fileMemory);

            _logger.LogInformation("🐍 Acquiring Python GIL...");
            
            // Parse using Python AST
            using (Py.GIL()) // Acquire Global Interpreter Lock
            {
                _logger.LogInformation("🐍 GIL acquired, importing Python ast module...");
                dynamic ast = Py.Import("ast");
                dynamic sys = Py.Import("sys");

                _logger.LogInformation("🐍 Python modules imported, parsing code...");
                
                try
                {
                    // Parse the Python code into an AST
                    dynamic tree = ast.parse(code, filePath);
                    _logger.LogInformation("🐍 AST parsing successful!");

                    // Extract imports
                    ExtractImports(tree, filePath, context, result);

                    // Extract classes
                    var classes = new List<dynamic>();
                    // Convert Python list to .NET enumerable explicitly
                    var treeBody = new List<dynamic>();
                    foreach (dynamic node in tree.body)
                    {
                        treeBody.Add(node);
                    }
                    
                    foreach (dynamic node in treeBody)
                    {
                        if (ast.ClassDef.Equals(node.__class__))
                        {
                            classes.Add(node);
                        }
                    }

                    _logger.LogInformation("🐍 Found {Count} classes", classes.Count);

                    foreach (dynamic classNode in classes)
                    {
                        await ExtractClassAsync(classNode, filePath, context, result, code, cancellationToken);
                    }

                    // Extract top-level functions
                    var functions = new List<dynamic>();
                    foreach (dynamic node in treeBody)
                    {
                        if (ast.FunctionDef.Equals(node.__class__) || ast.AsyncFunctionDef.Equals(node.__class__))
                        {
                            functions.Add(node);
                        }
                    }

                    _logger.LogInformation("🐍 Found {Count} top-level functions", functions.Count);

                    foreach (dynamic funcNode in functions)
                    {
                        await ExtractFunctionAsync(funcNode, null, filePath, context, result, code, cancellationToken);
                    }
                }
                catch (PythonException pyEx)
                {
                    _logger.LogError(pyEx, "Python AST parsing error for file: {FilePath}", filePath);
                    result.Errors.Add($"Python AST parsing error: {pyEx.Message}");
                }
            }

            // PATTERN DETECTION: Detect coding patterns (caching, retry, validation, etc.)
            _logger.LogInformation("🐍 Running pattern detection...");
            try
            {
                var pythonPatternDetector = new PythonPatternDetector();
                var detectedPatterns = pythonPatternDetector.DetectPatterns(code, filePath, context);
                
                if (detectedPatterns.Any())
                {
                    _logger.LogInformation("🐍 Detected {Count} patterns in {FilePath}", detectedPatterns.Count, filePath);
                    
                    // Store patterns in result metadata for indexing service to process
                    if (!result.CodeElements.First().Metadata.ContainsKey("detected_patterns"))
                    {
                        result.CodeElements.First().Metadata["detected_patterns"] = detectedPatterns;
                    }
                }
            }
            catch (Exception patternEx)
            {
                _logger.LogWarning(patternEx, "Pattern detection failed for {FilePath}", filePath);
            }

            return await Task.FromResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing Python code: {FilePath}", filePath);
            result.Errors.Add($"Error parsing Python code: {ex.Message}");
            return result;
        }
    }

    private void ExtractImports(dynamic tree, string filePath, string context, ParseResult result)
    {
        using (Py.GIL())
        {
            dynamic ast = Py.Import("ast");

            foreach (dynamic node in tree.body)
            {
                // Handle: import module
                if (ast.Import.Equals(node.__class__))
                {
                    foreach (dynamic alias in node.names)
                    {
                        string moduleName = alias.name.ToString();
                        result.Relationships.Add(new CodeRelationship
                        {
                            FromName = Path.GetFileNameWithoutExtension(filePath),
                            ToName = moduleName,
                            Type = RelationshipType.Imports,
                            Context = context,
                            Properties = new Dictionary<string, object>
                            {
                                ["line_number"] = (int)node.lineno,
                                ["import_type"] = "import"
                            }
                        });
                    }
                }
                // Handle: from module import name
                else if (ast.ImportFrom.Equals(node.__class__))
                {
                    string moduleName = node.module?.ToString() ?? "";
                    foreach (dynamic alias in node.names)
                    {
                        string importedName = alias.name.ToString();
                        string fullName = string.IsNullOrEmpty(moduleName) ? importedName : $"{moduleName}.{importedName}";
                        
                        result.Relationships.Add(new CodeRelationship
                        {
                            FromName = Path.GetFileNameWithoutExtension(filePath),
                            ToName = fullName,
                            Type = RelationshipType.Imports,
                            Context = context,
                            Properties = new Dictionary<string, object>
                            {
                                ["line_number"] = (int)node.lineno,
                                ["import_type"] = "from_import",
                                ["module"] = moduleName
                            }
                        });
                    }
                }
            }
        }
    }

    private async Task ExtractClassAsync(dynamic classNode, string filePath, string context, ParseResult result, string code, CancellationToken cancellationToken)
    {
        using (Py.GIL())
        {
            dynamic ast = Py.Import("ast");

            string className = classNode.name.ToString();
            int lineNumber = (int)classNode.lineno;

            // Extract docstring (summary)
            string docstring = ExtractDocstring(classNode);
            var summary = docstring;
            var purpose = summary.Length > 200 ? summary.Substring(0, 200) + "..." : summary;

            // Extract signature
            var signature = $"class {className}";
            var basesList = new List<dynamic>();
            if (classNode.bases != null)
            {
                foreach (dynamic baseNode in classNode.bases)
                {
                    basesList.Add(baseNode);
                }
            }
            
            if (basesList.Any())
            {
                var baseNames = new List<string>();
                foreach (dynamic baseNode in basesList)
                {
                    baseNames.Add(GetNodeName(baseNode));
                }
                signature += $"({string.Join(", ", baseNames)})";
            }

            // Extract decorators as tags
            var tags = new List<string> { "class" };
            var decoratorsList = new List<dynamic>();
            if (classNode.decorator_list != null)
            {
                foreach (dynamic decorator in classNode.decorator_list)
                {
                    decoratorsList.Add(decorator);
                }
                foreach (dynamic decorator in decoratorsList)
                {
                    tags.Add($"@{GetNodeName(decorator)}");
                }
            }

            // Extract base classes as dependencies
            var dependencies = new List<string>();
            if (basesList.Any())
            {
                foreach (dynamic baseNode in basesList)
                {
                    string baseName = GetNodeName(baseNode);
                    dependencies.Add(baseName);

                    result.Relationships.Add(new CodeRelationship
                    {
                        FromName = className,
                        ToName = baseName,
                        Type = RelationshipType.Inherits,
                        Context = context,
                        Properties = new Dictionary<string, object>
                        {
                            ["to_node_type"] = "Class",
                            ["line_number"] = lineNumber
                        }
                    });
                }
            }

            var classMemory = new CodeMemory
            {
                Type = CodeMemoryType.Class,
                Name = className,
                Content = GetNodeSource(classNode, code),
                FilePath = filePath,
                Context = context,
                LineNumber = lineNumber,
                Summary = summary,
                Signature = signature,
                Purpose = purpose,
                Tags = tags,
                Dependencies = dependencies,
                Metadata = new Dictionary<string, object>
                {
                    ["language"] = "python",
                    ["type"] = "class"
                }
            };

            result.CodeElements.Add(classMemory);

            // DEFINES relationship
            result.Relationships.Add(new CodeRelationship
            {
                FromName = Path.GetFileName(filePath),
                ToName = className,
                Type = RelationshipType.Defines,
                Context = context,
                Properties = new Dictionary<string, object>
                {
                    ["to_node_type"] = "Class",
                    ["line_number"] = lineNumber
                }
            });

            // Extract methods
            var classBodyList = new List<dynamic>();
            foreach (dynamic node in classNode.body)
            {
                classBodyList.Add(node);
            }
            
            foreach (dynamic node in classBodyList)
            {
                if (ast.FunctionDef.Equals(node.__class__) || ast.AsyncFunctionDef.Equals(node.__class__))
                {
                    await ExtractFunctionAsync(node, className, filePath, context, result, code, cancellationToken);
                }
            }
            
            _logger.LogInformation("🐍 Extracted class: {Name}, {MethodCount} methods", className, classBodyList.Count(n => ast.FunctionDef.Equals(n.__class__) || ast.AsyncFunctionDef.Equals(n.__class__)));

            await Task.CompletedTask;
        }
    }

    private async Task ExtractFunctionAsync(dynamic funcNode, string? parentClass, string filePath, string context, ParseResult result, string code, CancellationToken cancellationToken)
    {
        using (Py.GIL())
        {
            dynamic ast = Py.Import("ast");

            string funcName = funcNode.name.ToString();
            string fullFuncName = parentClass != null ? $"{parentClass}.{funcName}" : funcName;
            int lineNumber = (int)funcNode.lineno;
            bool isAsync = ast.AsyncFunctionDef.Equals(funcNode.__class__);

            // Extract docstring
            string docstring = ExtractDocstring(funcNode);
            var summary = docstring;
            var purpose = summary.Length > 200 ? summary.Substring(0, 200) + "..." : summary;

            // Extract signature
            var paramNames = new List<string>();
            if (funcNode.args != null)
            {
                dynamic argsNode = funcNode.args;
                
                // Regular args
                if (argsNode.args != null)
                {
                    foreach (dynamic arg in argsNode.args)
                    {
                        string argName = arg.arg.ToString();
                        string annotation = arg.annotation != null ? $": {GetNodeName(arg.annotation)}" : "";
                        paramNames.Add($"{argName}{annotation}");
                    }
                }
            }

            string returnType = funcNode.returns != null ? $" -> {GetNodeName(funcNode.returns)}" : "";
            string asyncModifier = isAsync ? "async " : "";
            var signature = $"{asyncModifier}def {funcName}({string.Join(", ", paramNames)}){returnType}";

            // Extract tags
            var tags = new List<string> { "function" };
            if (parentClass != null) tags.Add("method");
            if (isAsync) tags.Add("async");
            if (funcName.StartsWith("_") && !funcName.StartsWith("__")) tags.Add("private");
            if (funcName.StartsWith("__") && funcName.EndsWith("__")) tags.Add("magic");

            var funcDecoratorsList = new List<dynamic>();
            if (funcNode.decorator_list != null)
            {
                foreach (dynamic decorator in funcNode.decorator_list)
                {
                    funcDecoratorsList.Add(decorator);
                }
                foreach (dynamic decorator in funcDecoratorsList)
                {
                    tags.Add($"@{GetNodeName(decorator)}");
                }
            }

            // Extract dependencies (parameter types, return type)
            var dependencies = new List<string>();
            if (funcNode.args != null)
            {
                dynamic argsNode = funcNode.args;
                var argsList = new List<dynamic>();
                if (argsNode.args != null)
                {
                    foreach (dynamic arg in argsNode.args)
                    {
                        argsList.Add(arg);
                    }
                    foreach (dynamic arg in argsList)
                    {
                        if (arg.annotation != null)
                        {
                            string typeName = GetNodeName(arg.annotation);
                            if (!dependencies.Contains(typeName))
                            {
                                dependencies.Add(typeName);
                            }
                        }
                    }
                }
            }

            if (funcNode.returns != null)
            {
                string returnTypeName = GetNodeName(funcNode.returns);
                if (!dependencies.Contains(returnTypeName))
                {
                    dependencies.Add(returnTypeName);
                }
            }

            var methodMemory = new CodeMemory
            {
                Type = CodeMemoryType.Method,
                Name = fullFuncName,
                Content = GetNodeSource(funcNode, code),
                FilePath = filePath,
                Context = context,
                LineNumber = lineNumber,
                Summary = summary,
                Signature = signature,
                Purpose = purpose,
                Tags = tags,
                Dependencies = dependencies,
                Metadata = new Dictionary<string, object>
                {
                    ["language"] = "python",
                    ["type"] = isAsync ? "async_function" : "function",
                    ["parent_class"] = parentClass ?? "module"
                }
            };

            result.CodeElements.Add(methodMemory);

            // DEFINES relationship
            string parentName = parentClass ?? Path.GetFileName(filePath);
            result.Relationships.Add(new CodeRelationship
            {
                FromName = parentName,
                ToName = fullFuncName,
                Type = RelationshipType.Defines,
                Context = context,
                Properties = new Dictionary<string, object>
                {
                    ["to_node_type"] = "Method",
                    ["line_number"] = lineNumber
                }
            });

            // Extract function calls
            ExtractFunctionCalls(funcNode, fullFuncName, parentClass, context, result);

            // Extract Try/Except blocks
            ExtractExceptionHandling(funcNode, fullFuncName, context, result);

            _logger.LogInformation("🐍 Extracted function: {Name}, {Tags}", fullFuncName, string.Join(", ", tags));

            await Task.CompletedTask;
        }
    }

    private void ExtractFunctionCalls(dynamic funcNode, string callerName, string? parentClass, string context, ParseResult result)
    {
        using (Py.GIL())
        {
            dynamic ast = Py.Import("ast");

            // Walk the AST to find Call nodes
            var calls = new List<dynamic>();
            WalkAST(funcNode, ast.Call, calls);

            foreach (dynamic callNode in calls)
            {
                string calledName = GetNodeName(callNode.func);
                
                // Resolve self.method() to ClassName.method
                if (calledName.StartsWith("self.") && parentClass != null)
                {
                    calledName = $"{parentClass}.{calledName.Substring(5)}";
                }

                result.Relationships.Add(new CodeRelationship
                {
                    FromName = callerName,
                    ToName = calledName,
                    Type = RelationshipType.Calls,
                    Context = context,
                    Properties = new Dictionary<string, object>
                    {
                        ["to_node_type"] = "Method"
                    }
                });
            }
        }
    }

    private void ExtractExceptionHandling(dynamic funcNode, string funcName, string context, ParseResult result)
    {
        using (Py.GIL())
        {
            dynamic ast = Py.Import("ast");

            // Extract Try/Except (CATCHES)
            var tryNodes = new List<dynamic>();
            WalkAST(funcNode, ast.Try, tryNodes);

            foreach (dynamic tryNode in tryNodes)
            {
                if (tryNode.handlers != null)
                {
                    foreach (dynamic handler in tryNode.handlers)
                    {
                        if (handler.type != null)
                        {
                            string exceptionType = GetNodeName(handler.type);
                            result.Relationships.Add(new CodeRelationship
                            {
                                FromName = funcName,
                                ToName = exceptionType,
                                Type = RelationshipType.Catches,
                                Context = context,
                                Properties = new Dictionary<string, object>
                                {
                                    ["to_node_type"] = "Class"
                                }
                            });
                        }
                    }
                }
            }

            // Extract Raise (THROWS)
            var raiseNodes = new List<dynamic>();
            WalkAST(funcNode, ast.Raise, raiseNodes);

            foreach (dynamic raiseNode in raiseNodes)
            {
                if (raiseNode.exc != null)
                {
                    string exceptionType = GetNodeName(raiseNode.exc);
                    // Handle: raise CustomException() -> extract class name
                    if (exceptionType.Contains("("))
                    {
                        exceptionType = exceptionType.Substring(0, exceptionType.IndexOf("("));
                    }
                    
                    result.Relationships.Add(new CodeRelationship
                    {
                        FromName = funcName,
                        ToName = exceptionType,
                        Type = RelationshipType.Throws,
                        Context = context,
                        Properties = new Dictionary<string, object>
                        {
                            ["to_node_type"] = "Class"
                        }
                    });
                }
            }
        }
    }

    private void WalkAST(dynamic node, dynamic targetType, List<dynamic> results)
    {
        using (Py.GIL())
        {
            dynamic ast = Py.Import("ast");

            if (targetType.Equals(node.__class__))
            {
                results.Add(node);
            }

            // Recursively walk child nodes
            foreach (dynamic field in ast.iter_fields(node))
            {
                string fieldName = field[0].ToString();
                dynamic fieldValue = field[1];

                if (fieldValue != null)
                {
                    // Check if it's a node
                    try
                    {
                        // Check if fieldValue is an AST node
                        var mro = fieldValue.__class__.__mro__;
                        bool isNode = false;
                        foreach (dynamic baseClass in mro)
                        {
                            if (baseClass.Equals(ast.AST))
                            {
                                isNode = true;
                                break;
                            }
                        }
                        
                        if (isNode)
                        {
                            WalkAST(fieldValue, targetType, results);
                        }
                    }
                    catch
                    {
                        // Try as list of nodes
                        try
                        {
                            foreach (dynamic child in fieldValue)
                            {
                                try
                                {
                                    // Check if child is an AST node
                                    var childMro = child.__class__.__mro__;
                                    bool isChildNode = false;
                                    foreach (dynamic baseClass in childMro)
                                    {
                                        if (baseClass.Equals(ast.AST))
                                        {
                                            isChildNode = true;
                                            break;
                                        }
                                    }
                                    
                                    if (isChildNode)
                                    {
                                        WalkAST(child, targetType, results);
                                    }
                                }
                                catch { }
                            }
                        }
                        catch { }
                    }
                }
            }
        }
    }

    private string ExtractDocstring(dynamic node)
    {
        using (Py.GIL())
        {
            dynamic ast = Py.Import("ast");

            try
            {
                // Check if first statement in body is a string (docstring)
                if (node.body != null && ((IEnumerable<dynamic>)node.body).Any())
                {
                    dynamic firstNode = ((IEnumerable<dynamic>)node.body).First();
                    if (ast.Expr.Equals(firstNode.__class__))
                    {
                        if (ast.Str.Equals(firstNode.value.__class__) || ast.Constant.Equals(firstNode.value.__class__))
                        {
                            return firstNode.value.s?.ToString() ?? firstNode.value.value?.ToString() ?? "";
                        }
                    }
                }
            }
            catch { }

            return string.Empty;
        }
    }

    private string GetNodeName(dynamic node)
    {
        using (Py.GIL())
        {
            dynamic ast = Py.Import("ast");

            try
            {
                if (ast.Name.Equals(node.__class__))
                {
                    return node.id.ToString();
                }
                else if (ast.Attribute.Equals(node.__class__))
                {
                    string obj = GetNodeName(node.value);
                    string attr = node.attr.ToString();
                    return $"{obj}.{attr}";
                }
                else if (ast.Call.Equals(node.__class__))
                {
                    return GetNodeName(node.func);
                }
                else if (ast.Subscript.Equals(node.__class__))
                {
                    return GetNodeName(node.value);
                }
                else if (ast.Constant.Equals(node.__class__))
                {
                    return node.value?.ToString() ?? "Unknown";
                }
            }
            catch { }

            return "Unknown";
        }
    }

    private string GetNodeSource(dynamic node, string code)
    {
        try
        {
            int startLine = (int)node.lineno - 1; // 0-indexed
            int endLine = (int)node.end_lineno; // Inclusive, so no -1

            var lines = code.Split('\n');
            if (startLine >= 0 && endLine <= lines.Length)
            {
                return string.Join("\n", lines.Skip(startLine).Take(endLine - startLine));
            }
        }
        catch { }

        return string.Empty;
    }

    private string DetermineContext(string filePath)
    {
        var parts = filePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        
        // Look for common project root indicators
        for (int i = parts.Length - 1; i >= 0; i--)
        {
            if (parts[i] == "src" || parts[i] == "source" || File.Exists(Path.Combine(string.Join(Path.DirectorySeparatorChar, parts.Take(i + 1)), "setup.py")))
            {
                return i < parts.Length - 1 ? parts[i + 1] : parts[i];
            }
        }
        
        // Fallback to directory name containing the file
        return parts.Length >= 2 ? parts[^2] : "default";
    }
}

