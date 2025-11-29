using System.Text.RegularExpressions;
using MemoryAgent.Server.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MemoryAgent.Server.CodeAnalysis;

/// <summary>
/// Parses C# code using Roslyn to extract classes, methods, properties, and relationships
/// </summary>
public class RoslynParser : ICodeParser
{
    private readonly ILogger<RoslynParser> _logger;
    private readonly ILoggerFactory _loggerFactory;

    public RoslynParser(ILogger<RoslynParser> logger, ILoggerFactory loggerFactory)
    {
        _logger = logger;
        _loggerFactory = loggerFactory;
    }
    
    private static bool IsConfigFile(string fileName)
    {
        return fileName == "package.json" ||
               fileName == "package-lock.json" ||
               fileName == "config.json" ||
               fileName.StartsWith("appsettings") ||
               fileName == "tsconfig.json";
    }

    public async Task<ParseResult> ParseFileAsync(string filePath, string? context = null, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return new ParseResult { Errors = { $"File not found: {filePath}" } };
            }

            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            var fileName = Path.GetFileName(filePath).ToLowerInvariant();
            
            // Route to appropriate parser based on file extension or name
            return extension switch
            {
                ".cshtml" or ".razor" => await Task.Run(() => RazorParser.ParseRazorFile(filePath, context, _loggerFactory), cancellationToken),
                ".py" => await Task.Run(() => PythonParser.ParsePythonFile(filePath, context), cancellationToken),
                ".md" or ".markdown" => await new MarkdownParser(_loggerFactory.CreateLogger<MarkdownParser>()).ParseFileAsync(filePath, context, cancellationToken),
                ".css" or ".scss" or ".less" => await Task.Run(() => CssParser.ParseCssFile(filePath, context), cancellationToken),
                ".js" or ".jsx" or ".ts" or ".tsx" => await Task.Run(() => JavaScriptParser.ParseJavaScriptFile(filePath, context), cancellationToken),
                ".vb" => await Task.Run(() => VBNetParser.ParseVBNetFile(filePath, context), cancellationToken),
                ".csproj" or ".vbproj" or ".fsproj" or ".sln" => await Task.Run(() => ProjectFileParser.ParseProjectFile(filePath, context), cancellationToken),
                ".json" when IsConfigFile(fileName) => await Task.Run(() => ConfigFileParser.ParseConfigFile(filePath, context), cancellationToken),
                ".yml" or ".yaml" when fileName.Contains("docker-compose") => await Task.Run(() => DockerfileParser.ParseDockerfile(filePath, context), cancellationToken),
                ".config" when fileName == "web.config" => await Task.Run(() => ConfigFileParser.ParseConfigFile(filePath, context), cancellationToken),
                _ when fileName == "dockerfile" || fileName.EndsWith(".dockerfile") => await Task.Run(() => DockerfileParser.ParseDockerfile(filePath, context), cancellationToken),
                ".bicep" => await new BicepParser(_loggerFactory.CreateLogger<BicepParser>()).ParseFileAsync(filePath, context, cancellationToken),
                ".json" when !IsConfigFile(fileName) => await new JsonParser(_loggerFactory.CreateLogger<JsonParser>()).ParseFileAsync(filePath, context, cancellationToken),
                ".cs" => await ParseCSharpFileAsync(filePath, context, cancellationToken),
                _ => new ParseResult { Errors = { $"Unsupported file type: {extension}" } }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing file: {FilePath}", filePath);
            return new ParseResult { Errors = { $"Error parsing file: {ex.Message}" } };
        }
    }
    
    private async Task<ParseResult> ParseCSharpFileAsync(string filePath, string? context, CancellationToken cancellationToken)
    {
        var code = await File.ReadAllTextAsync(filePath, cancellationToken);
        return await ParseCodeAsync(code, filePath, context, cancellationToken);
    }

    public async Task<ParseResult> ParseCodeAsync(string code, string filePath, string? context = null, CancellationToken cancellationToken = default)
    {
        var result = new ParseResult();

        try
        {
            // Parse the code
            var tree = CSharpSyntaxTree.ParseText(code, cancellationToken: cancellationToken);
            var root = tree.GetRoot(cancellationToken);

            // Determine context from file path if not provided
            if (string.IsNullOrWhiteSpace(context))
            {
                context = DetermineContext(filePath);
            }

            // Extract file info
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
                    ["size"] = code.Length,
                    ["language"] = "csharp",
                    ["last_modified"] = fileInfo.LastWriteTimeUtc.ToString("O"),
                    ["line_count"] = code.Split('\n').Length
                }
            };
            result.CodeElements.Add(fileMemory);

            // Extract using directives (imports)
            ExtractUsingDirectives(root, filePath, context, result);

            // SEMANTIC CHUNKING: Extract file-level configuration patterns
            ExtractSwaggerConfig(root, filePath, context, result);
            ExtractCorsPolicies(root, filePath, context, result);
            ExtractRateLimiting(root, filePath, context, result);

            // Extract namespaces, classes, interfaces
            var namespaceDeclarations = root.DescendantNodes().OfType<BaseNamespaceDeclarationSyntax>();
            foreach (var namespaceDecl in namespaceDeclarations)
            {
                var namespaceName = namespaceDecl.Name.ToString();

                // Extract classes
                var classDeclarations = namespaceDecl.DescendantNodes().OfType<ClassDeclarationSyntax>();
                foreach (var classDecl in classDeclarations)
                {
                    ExtractClass(classDecl, namespaceName, filePath, context, result);
                }

                // Extract interfaces
                var interfaceDeclarations = namespaceDecl.DescendantNodes().OfType<InterfaceDeclarationSyntax>();
                foreach (var interfaceDecl in interfaceDeclarations)
                {
                    ExtractInterface(interfaceDecl, namespaceName, filePath, context, result);
                }
            }

            // PATTERN DETECTION: Detect code patterns using multiple detectors
            try
            {
                var allDetectedPatterns = new List<CodePattern>();
                
                // 1. Enhanced C# patterns (Azure best practices)
                var enhancedDetector = new CSharpPatternDetectorEnhanced();
                var enhancedPatterns = enhancedDetector.DetectPatterns(code, filePath, context);
                allDetectedPatterns.AddRange(enhancedPatterns);
                
                // 2. Agent Framework patterns (Semantic Kernel, AutoGen, Agent Lightning, etc.)
                var agentFrameworkDetector = new AgentFrameworkPatternDetector(_loggerFactory.CreateLogger<AgentFrameworkPatternDetector>());
                var agentPatterns = await agentFrameworkDetector.DetectPatternsAsync(filePath, context, code, cancellationToken);
                allDetectedPatterns.AddRange(agentPatterns);
                
                // 3. AG-UI patterns (Agent UI Protocol integration)
                var aguiDetector = new AGUIPatternDetector(_loggerFactory.CreateLogger<AGUIPatternDetector>());
                var aguiPatterns = await aguiDetector.DetectPatternsAsync(filePath, context, code, cancellationToken);
                allDetectedPatterns.AddRange(aguiPatterns);
                
                // AI AGENT CORE PATTERN DETECTION: Detect AI agent patterns (prompts, memory, tools, RAG, safety, cost)
                var aiAgentDetector = new AIAgentPatternDetector(_loggerFactory.CreateLogger<AIAgentPatternDetector>());
                var aiAgentPatterns = await aiAgentDetector.DetectPatternsAsync(filePath, context, code, cancellationToken);
                allDetectedPatterns.AddRange(aiAgentPatterns);
                
                // PLUGIN ARCHITECTURE PATTERN DETECTION: Detect plugin patterns (loading, MEF, lifecycle, communication, security, versioning)
                var pluginDetector = new PluginArchitecturePatternDetector(_loggerFactory.CreateLogger<PluginArchitecturePatternDetector>());
                var pluginPatterns = await pluginDetector.DetectPatternsAsync(filePath, context, code, cancellationToken);
                allDetectedPatterns.AddRange(pluginPatterns);
                
                // STATE MANAGEMENT PATTERN DETECTION: Detect Blazor & ASP.NET Core state management patterns (server-side, client-side, component, persistence, security)
                // TODO: StateManagementPatternDetector needs to be recreated
                // var stateDetector = new StateManagementPatternDetector();
                // var statePatterns = stateDetector.DetectAllPatterns(root);
                // allDetectedPatterns.AddRange(statePatterns);
                
                if (allDetectedPatterns.Any())
                {
                    _logger.LogDebug("Detected {Count} patterns in {FilePath} ({Enhanced} enhanced, {Agent} agent, {AGUI} AG-UI)", 
                        allDetectedPatterns.Count, filePath, enhancedPatterns.Count, agentPatterns.Count, aguiPatterns.Count);
                    
                    // Store patterns in result metadata for indexing service to process
                    if (!result.CodeElements.First().Metadata.ContainsKey("detected_patterns"))
                    {
                        result.CodeElements.First().Metadata["detected_patterns"] = allDetectedPatterns;
                    }
                }
            }
            catch (Exception patternEx)
            {
                _logger.LogWarning(patternEx, "Failed to detect patterns in {FilePath}", filePath);
                // Don't fail the whole parse if pattern detection fails
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing code for file: {FilePath}", filePath);
            result.Errors.Add($"Error parsing code: {ex.Message}");
            return result;
        }
    }

    private void ExtractClass(
        ClassDeclarationSyntax classDecl,
        string namespaceName,
        string filePath,
        string context,
        ParseResult result)
    {
        var className = classDecl.Identifier.Text;
        var fullName = $"{namespaceName}.{className}";
        var lineNumber = classDecl.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

        // Calculate class metrics
        var linesOfCode = ComplexityAnalyzer.CalculateLinesOfCode(classDecl);
        var methodCount = classDecl.Members.OfType<MethodDeclarationSyntax>().Count();
        var propertyCount = classDecl.Members.OfType<PropertyDeclarationSyntax>().Count();
        var fieldCount = classDecl.Members.OfType<FieldDeclarationSyntax>().Count();
        var isPublicApi = ComplexityAnalyzer.IsPublicApi(classDecl);

        // Extract semantic metadata for smart embeddings
        var summary = ExtractXmlSummary(classDecl);
        var signature = BuildClassSignature(classDecl);
        var tags = ExtractClassTags(classDecl, namespaceName);
        var dependencies = ExtractClassDependencies(classDecl);
        
        // Create class memory
        var classMemory = new CodeMemory
        {
            Type = CodeMemoryType.Class,
            Name = fullName,
            Content = classDecl.ToString(),
            FilePath = filePath,
            Context = context,
            LineNumber = lineNumber,
            
            // NEW: Smart embedding fields
            Summary = summary,
            Signature = signature,
            Purpose = summary, // For classes, summary == purpose
            Tags = tags,
            Dependencies = dependencies,
            
            Metadata = new Dictionary<string, object>
            {
                ["namespace"] = namespaceName,
                ["is_abstract"] = classDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.AbstractKeyword)),
                ["is_static"] = classDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword)),
                ["is_sealed"] = classDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.SealedKeyword)),
                ["access_modifier"] = GetAccessModifier(classDecl.Modifiers),
                ["language"] = "csharp",
                ["layer"] = DetermineLayer(className, namespaceName),
                ["bounded_context"] = DetermineBoundedContext(namespaceName),
                
                // Class metrics
                ["lines_of_code"] = linesOfCode,
                ["method_count"] = methodCount,
                ["property_count"] = propertyCount,
                ["field_count"] = fieldCount,
                ["is_god_class"] = linesOfCode > 1000, // God class smell
                
                // API visibility
                ["is_public_api"] = isPublicApi,
                ["is_internal"] = !isPublicApi
            }
        };
        
        // Add framework metadata if applicable
        if (className.EndsWith("Controller"))
        {
            classMemory.Metadata["framework"] = "aspnet-core";
            classMemory.Metadata["chunk_type"] = "controller";
        }
        else if (className.Contains("DbContext"))
        {
            classMemory.Metadata["framework"] = "ef-core";
            classMemory.Metadata["chunk_type"] = "dbcontext";
        }
        else if (className.EndsWith("Validator"))
        {
            classMemory.Metadata["framework"] = "fluentvalidation";
            classMemory.Metadata["chunk_type"] = "validator";
        }
        
        result.CodeElements.Add(classMemory);

        // SEMANTIC CHUNKING: Extract validation logic (DataAnnotations + FluentValidation)
        ExtractValidationLogic(classDecl, fullName, filePath, context, result);

        // SEMANTIC CHUNKING: Extract background jobs (Hangfire + IHostedService)
        ExtractBackgroundJobs(classDecl, fullName, filePath, context, result);

        // SEMANTIC CHUNKING: Extract MediatR handlers (Commands/Queries/Events)
        ExtractMediatRHandlers(classDecl, fullName, filePath, context, result);

        // SEMANTIC CHUNKING: Extract AutoMapper profiles
        ExtractAutoMapperProfiles(classDecl, fullName, filePath, context, result);

        // SEMANTIC CHUNKING: Extract repository patterns
        ExtractRepositoryPatterns(classDecl, fullName, context, result);

        // SEMANTIC CHUNKING: Extract health checks
        ExtractHealthChecks(classDecl, fullName, filePath, context, result);

        // SEMANTIC CHUNKING: Extract API versioning
        ExtractApiVersioning(classDecl, fullName, filePath, context, result);

        // SEMANTIC CHUNKING: Extract exception filters
        ExtractExceptionFilters(classDecl, fullName, filePath, context, result);

        // SEMANTIC CHUNKING: Extract model binders
        ExtractModelBinders(classDecl, fullName, filePath, context, result);

        // SEMANTIC CHUNKING: Extract action filters
        ExtractActionFilters(classDecl, fullName, filePath, context, result);

        // Extract base classes and interfaces
        if (classDecl.BaseList != null)
        {
            foreach (var baseType in classDecl.BaseList.Types)
            {
                var baseTypeName = baseType.Type.ToString();
                var relType = IsInterface(baseTypeName) ? RelationshipType.Implements : RelationshipType.Inherits;
                
                result.Relationships.Add(new CodeRelationship
                {
                    FromName = fullName,
                    ToName = baseTypeName,
                    Type = relType,
                    Context = context
                });
            }
        }

        // Build a type map for this class (fields + DI injected types)
        var classTypeMap = BuildClassTypeMap(classDecl);
        
        // Extract methods
        var methods = classDecl.Members.OfType<MethodDeclarationSyntax>();
        foreach (var method in methods)
        {
            ExtractMethod(method, fullName, className, filePath, context, result, classTypeMap);
        }

        // Extract properties
        var properties = classDecl.Members.OfType<PropertyDeclarationSyntax>();
        foreach (var property in properties)
        {
            ExtractProperty(property, fullName, className, filePath, context, result);
        }

        // Extract constructor injection (DI)
        ExtractConstructorInjection(classDecl, fullName, context, result);
        
        // Extract attributes on class
        ExtractAttributes(classDecl.AttributeLists, fullName, context, result);
        
        // Extract generic type parameters
        ExtractGenericTypes(classDecl.TypeParameterList, fullName, context, result);
        
        // Also find member access and object creation for dependencies
        ExtractDependencies(classDecl, fullName, context, result);
    }

    private void ExtractInterface(
        InterfaceDeclarationSyntax interfaceDecl,
        string namespaceName,
        string filePath,
        string context,
        ParseResult result)
    {
        var interfaceName = interfaceDecl.Identifier.Text;
        var fullName = $"{namespaceName}.{interfaceName}";
        var lineNumber = interfaceDecl.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

        var interfaceMemory = new CodeMemory
        {
            Type = CodeMemoryType.Interface,
            Name = fullName,
            Content = interfaceDecl.ToString(),
            FilePath = filePath,
            Context = context,
            LineNumber = lineNumber,
            Metadata = new Dictionary<string, object>
            {
                ["namespace"] = namespaceName,
                ["access_modifier"] = GetAccessModifier(interfaceDecl.Modifiers)
            }
        };
        result.CodeElements.Add(interfaceMemory);

        // Extract base interfaces
        if (interfaceDecl.BaseList != null)
        {
            foreach (var baseType in interfaceDecl.BaseList.Types)
            {
                result.Relationships.Add(new CodeRelationship
                {
                    FromName = fullName,
                    ToName = baseType.Type.ToString(),
                    Type = RelationshipType.Inherits,
                    Context = context
                });
            }
        }
    }

    private void ExtractMethod(
        MethodDeclarationSyntax methodDecl,
        string fullClassName,
        string className,
        string filePath,
        string context,
        ParseResult result,
        Dictionary<string, string>? classTypeMap = null)
    {
        var methodName = methodDecl.Identifier.Text;
        var lineNumber = methodDecl.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

        // Calculate complexity metrics
        var cyclomaticComplexity = ComplexityAnalyzer.CalculateCyclomaticComplexity(methodDecl);
        var cognitiveComplexity = ComplexityAnalyzer.CalculateCognitiveComplexity(methodDecl);
        var linesOfCode = ComplexityAnalyzer.CalculateLinesOfCode(methodDecl);
        var codeSmells = ComplexityAnalyzer.DetectCodeSmells(methodDecl);
        var exceptionTypes = ComplexityAnalyzer.ExtractExceptionTypes(methodDecl);
        var dbCallCount = ComplexityAnalyzer.CountDatabaseCalls(methodDecl);
        var hasHttpCalls = ComplexityAnalyzer.HasHttpCalls(methodDecl);
        var hasLogging = ComplexityAnalyzer.HasLogging(methodDecl);
        var isPublicApi = ComplexityAnalyzer.IsPublicApi(methodDecl);
        var isAsync = methodDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.AsyncKeyword));

        // Detect if this is a test method
        var testAttributes = new[] { "Test", "Fact", "Theory", "TestMethod", "TestCase" };
        var isTestMethod = methodDecl.AttributeLists
            .SelectMany(al => al.Attributes)
            .Any(attr => testAttributes.Any(t => attr.Name.ToString().Contains(t)));

        // Extract semantic metadata for smart embeddings
        var summary = ExtractXmlSummary(methodDecl);
        var signature = BuildMethodSignature(methodDecl);
        var tags = ExtractMethodTags(methodDecl);
        var dependencies = ExtractMethodDependencies(methodDecl);

        var methodMemory = new CodeMemory
        {
            Type = isTestMethod ? CodeMemoryType.Test : CodeMemoryType.Method,
            Name = $"{fullClassName}.{methodName}",
            Content = methodDecl.ToString(),
            FilePath = filePath,
            Context = context,
            LineNumber = lineNumber,
            
            // NEW: Smart embedding fields
            Summary = summary,
            Signature = signature,
            Purpose = summary, // For methods, summary == purpose
            Tags = tags,
            Dependencies = dependencies,
            
            Metadata = new Dictionary<string, object>
            {
                ["class_name"] = fullClassName,
                ["return_type"] = methodDecl.ReturnType.ToString(),
                ["is_async"] = isAsync,
                ["is_static"] = methodDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword)),
                ["is_virtual"] = methodDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.VirtualKeyword)),
                ["is_override"] = methodDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.OverrideKeyword)),
                ["access_modifier"] = GetAccessModifier(methodDecl.Modifiers),
                ["parameter_count"] = methodDecl.ParameterList.Parameters.Count,
                
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
                
                // API visibility
                ["is_public_api"] = isPublicApi,
                ["is_internal"] = !isPublicApi,
                
                // Test metadata (if test)
                ["is_test"] = isTestMethod
            }
        };

        // Add test-specific metadata
        if (isTestMethod)
        {
            methodMemory.Metadata["test_framework"] = DetectTestFramework(methodDecl);
            methodMemory.Metadata["assertion_count"] = methodDecl.DescendantNodes()
                .Count(n => n.ToString().Contains("Assert") || n.ToString().Contains("Should"));
        }

        result.CodeElements.Add(methodMemory);

        // Create DEFINES relationship
        result.Relationships.Add(new CodeRelationship
        {
            FromName = fullClassName,
            ToName = $"{fullClassName}.{methodName}",
            Type = RelationshipType.Defines,
            Context = context
        });

        // Extract method parameter types
        ExtractMethodParameterTypes(methodDecl, $"{fullClassName}.{methodName}", context, result);
        
        // Extract return type
        ExtractMethodReturnType(methodDecl, $"{fullClassName}.{methodName}", context, result);
        
        // Extract method calls with type resolution
        ExtractMethodCalls(methodDecl, $"{fullClassName}.{methodName}", context, result, classTypeMap);
        
        // Extract attributes on method
        ExtractAttributes(methodDecl.AttributeLists, $"{fullClassName}.{methodName}", context, result);
        
        // Extract exception handling
        ExtractExceptionHandling(methodDecl, $"{fullClassName}.{methodName}", context, result);
        
        // SEMANTIC CHUNKING: Enhance with ASP.NET action method semantics
        EnhanceWithActionMethodSemantics(methodDecl, fullClassName, methodName, methodMemory, result, context);
        
        // SEMANTIC CHUNKING: Enhance with EF query semantics
        EnhanceWithEFQuerySemantics(methodDecl, $"{fullClassName}.{methodName}", methodMemory, result, context);

        // SEMANTIC CHUNKING: Enhance with filters/caching/rate limiting
        EnhanceMethodWithAttributes(methodDecl, methodMemory, result, context);
        
        // SEMANTIC CHUNKING: Extract DI registrations (for Program.cs/Startup.cs)
        ExtractDIRegistrations(methodDecl, filePath, context, result);

        // SEMANTIC CHUNKING: Extract middleware pipeline
        ExtractMiddlewarePipeline(methodDecl, filePath, context, result);

        // SEMANTIC CHUNKING: Extract authorization policies
        ExtractAuthorizationPolicies(methodDecl, filePath, context, result);

        // SEMANTIC CHUNKING: Extract configuration binding
        ExtractConfigurationBinding(methodDecl, filePath, context, result);

        // SEMANTIC CHUNKING: Extract response caching
        ExtractResponseCaching(methodDecl, $"{fullClassName}.{methodName}", filePath, context, result);
    }

    private void ExtractProperty(
        PropertyDeclarationSyntax propertyDecl,
        string fullClassName,
        string className,
        string filePath,
        string context,
        ParseResult result)
    {
        var propertyName = propertyDecl.Identifier.Text;
        var lineNumber = propertyDecl.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

        var hasGetter = propertyDecl.AccessorList?.Accessors.Any(a => a.IsKind(SyntaxKind.GetAccessorDeclaration)) ?? false;
        var hasSetter = propertyDecl.AccessorList?.Accessors.Any(a => a.IsKind(SyntaxKind.SetAccessorDeclaration)) ?? false;

        var propertyMemory = new CodeMemory
        {
            Type = CodeMemoryType.Property,
            Name = $"{fullClassName}.{propertyName}",
            Content = propertyDecl.ToString(),
            FilePath = filePath,
            Context = context,
            LineNumber = lineNumber,
            Metadata = new Dictionary<string, object>
            {
                ["class_name"] = fullClassName,
                ["type"] = propertyDecl.Type.ToString(),
                ["has_getter"] = hasGetter,
                ["has_setter"] = hasSetter,
                ["access_modifier"] = GetAccessModifier(propertyDecl.Modifiers)
            }
        };
        result.CodeElements.Add(propertyMemory);

        // Create DEFINES relationship
        result.Relationships.Add(new CodeRelationship
        {
            FromName = fullClassName,
            ToName = $"{fullClassName}.{propertyName}",
            Type = RelationshipType.Defines,
            Context = context
        });

        // Extract property type dependency
        ExtractPropertyType(propertyDecl, $"{fullClassName}.{propertyName}", context, result);
        
        // Extract attributes on property
        ExtractAttributes(propertyDecl.AttributeLists, $"{fullClassName}.{propertyName}", context, result);
    }

    private void ExtractDependencies(ClassDeclarationSyntax classDecl, string fullClassName, string context, ParseResult result)
    {
        // Find object creation expressions
        var objectCreations = classDecl.DescendantNodes().OfType<ObjectCreationExpressionSyntax>();
        foreach (var creation in objectCreations)
        {
            var typeName = creation.Type.ToString();
            if (!string.IsNullOrWhiteSpace(typeName) && !IsPrimitiveType(typeName))
            {
                result.Relationships.Add(new CodeRelationship
                {
                    FromName = fullClassName,
                    ToName = typeName,
                    Type = RelationshipType.Uses,
                    Context = context,
                    Properties = new Dictionary<string, object> { ["count"] = 1 }
                });
            }
        }

        // Find type references in member declarations
        var fieldDeclarations = classDecl.Members.OfType<FieldDeclarationSyntax>();
        foreach (var field in fieldDeclarations)
        {
            var typeName = field.Declaration.Type.ToString();
            if (!string.IsNullOrWhiteSpace(typeName) && !IsPrimitiveType(typeName))
            {
                result.Relationships.Add(new CodeRelationship
                {
                    FromName = fullClassName,
                    ToName = typeName,
                    Type = RelationshipType.Uses,
                    Context = context
                });
            }
        }
    }

    private static string GetAccessModifier(SyntaxTokenList modifiers)
    {
        if (modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword))) return "public";
        if (modifiers.Any(m => m.IsKind(SyntaxKind.PrivateKeyword))) return "private";
        if (modifiers.Any(m => m.IsKind(SyntaxKind.ProtectedKeyword))) return "protected";
        if (modifiers.Any(m => m.IsKind(SyntaxKind.InternalKeyword))) return "internal";
        return "private"; // default
    }

    private static bool IsInterface(string typeName)
    {
        // Simple heuristic: interfaces typically start with 'I' followed by uppercase letter
        return typeName.Length > 1 && typeName[0] == 'I' && char.IsUpper(typeName[1]);
    }

    private static bool IsPrimitiveType(string typeName)
    {
        var primitives = new HashSet<string>
        {
            "int", "long", "short", "byte", "sbyte",
            "uint", "ulong", "ushort",
            "float", "double", "decimal",
            "bool", "char", "string", "object",
            "void", "var", "dynamic"
        };
        
        return primitives.Contains(typeName.ToLowerInvariant());
    }

    private static string DetectTestFramework(MethodDeclarationSyntax methodDecl)
    {
        var attributes = methodDecl.AttributeLists.SelectMany(al => al.Attributes).Select(a => a.Name.ToString());
        
        if (attributes.Any(a => a.Contains("Fact") || a.Contains("Theory")))
            return "xunit";
        if (attributes.Any(a => a.Contains("Test") && !a.Contains("TestMethod")))
            return "nunit";
        if (attributes.Any(a => a.Contains("TestMethod")))
            return "mstest";
        
        return "unknown";
    }

    private static string DetermineContext(string filePath)
    {
        // Extract context from file path
        // E.g., "E:\GitHub\MemoryAgent\MemoryAgent.Server\Services\GraphService.cs" 
        // -> "MemoryAgent.Server.Services"
        
        var directory = Path.GetDirectoryName(filePath) ?? "";
        var parts = directory.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        
        // Find the project root (contains .csproj)
        var relevantParts = new List<string>();
        bool foundProject = false;
        
        for (int i = parts.Length - 1; i >= 0; i--)
        {
            relevantParts.Insert(0, parts[i]);
            
            // Check if this directory contains a .csproj file
            var testPath = string.Join(Path.DirectorySeparatorChar, parts.Take(i + 1));
            if (Directory.Exists(testPath) && Directory.GetFiles(testPath, "*.csproj").Any())
            {
                foundProject = true;
                break;
            }
        }

        return foundProject ? string.Join(".", relevantParts) : Path.GetFileNameWithoutExtension(filePath);
    }

    // ==================== NEW EXTRACTION METHODS ====================

    /// <summary>
    /// Extract using directives (imports) from the file
    /// </summary>
    private void ExtractUsingDirectives(SyntaxNode root, string filePath, string context, ParseResult result)
    {
        var usingDirectives = root.DescendantNodes().OfType<UsingDirectiveSyntax>();
        
        foreach (var usingDirective in usingDirectives)
        {
            var namespaceName = usingDirective.Name?.ToString();
            if (!string.IsNullOrWhiteSpace(namespaceName))
            {
                result.Relationships.Add(new CodeRelationship
                {
                    FromName = filePath,
                    ToName = namespaceName,
                    Type = RelationshipType.Imports,
                    Context = context,
                    Properties = new Dictionary<string, object>
                    {
                        ["is_static"] = usingDirective.StaticKeyword.IsKind(SyntaxKind.StaticKeyword),
                        ["alias"] = usingDirective.Alias?.Name.ToString() ?? ""
                    }
                });
            }
        }
    }

    /// <summary>
    /// Build a type map for the class: field/parameter names -> their types
    /// This is used to resolve method calls like _repository.Save() to IUserRepository.Save()
    /// </summary>
    private Dictionary<string, string> BuildClassTypeMap(ClassDeclarationSyntax classDecl)
    {
        var typeMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        
        // Extract field declarations and their types
        var fields = classDecl.Members.OfType<FieldDeclarationSyntax>();
        foreach (var field in fields)
        {
            var fieldType = field.Declaration.Type.ToString();
            
            foreach (var variable in field.Declaration.Variables)
            {
                var fieldName = variable.Identifier.Text;
                if (!string.IsNullOrWhiteSpace(fieldType) && !IsPrimitiveType(fieldType))
                {
                    typeMap[fieldName] = CleanTypeName(fieldType);
                }
            }
        }
        
        // Extract constructor parameters (DI injections)
        var constructors = classDecl.Members.OfType<ConstructorDeclarationSyntax>();
        foreach (var constructor in constructors)
        {
            foreach (var parameter in constructor.ParameterList.Parameters)
            {
                var paramName = parameter.Identifier.Text;
                var paramType = parameter.Type?.ToString();
                
                if (!string.IsNullOrWhiteSpace(paramType) && !IsPrimitiveType(paramType))
                {
                    typeMap[paramName] = CleanTypeName(paramType);
                    
                    // Also map common DI field patterns: _repository, _service, etc.
                    // Constructor: MyService(IUserRepository repository)
                    // Field: private readonly IUserRepository _repository;
                    var fieldName = $"_{paramName}";
                    typeMap[fieldName] = CleanTypeName(paramType);
                }
            }
        }
        
        // Extract properties and their types
        var properties = classDecl.Members.OfType<PropertyDeclarationSyntax>();
        foreach (var property in properties)
        {
            var propName = property.Identifier.Text;
            var propType = property.Type.ToString();
            
            if (!string.IsNullOrWhiteSpace(propType) && !IsPrimitiveType(propType))
            {
                typeMap[propName] = CleanTypeName(propType);
            }
        }
        
        return typeMap;
    }

    /// <summary>
    /// Clean type name by removing nullable markers and extracting base type
    /// </summary>
    private string CleanTypeName(string typeName)
    {
        // Remove nullable marker: IUserRepository? -> IUserRepository
        typeName = typeName.TrimEnd('?');
        
        // Extract base type from generics: List<User> -> List
        var genericIndex = typeName.IndexOf('<');
        if (genericIndex > 0)
        {
            return typeName.Substring(0, genericIndex).Trim();
        }
        
        return typeName.Trim();
    }

    /// <summary>
    /// Extract constructor injection dependencies
    /// </summary>
    private void ExtractConstructorInjection(ClassDeclarationSyntax classDecl, string fullClassName, string context, ParseResult result)
    {
        var constructors = classDecl.Members.OfType<ConstructorDeclarationSyntax>();
        
        foreach (var constructor in constructors)
        {
            foreach (var parameter in constructor.ParameterList.Parameters)
            {
                var paramType = parameter.Type?.ToString();
                if (!string.IsNullOrWhiteSpace(paramType) && !IsPrimitiveType(paramType))
                {
                    result.Relationships.Add(new CodeRelationship
                    {
                        FromName = fullClassName,
                        ToName = paramType,
                        Type = RelationshipType.Injects,
                        Context = context,
                        Properties = new Dictionary<string, object>
                        {
                            ["parameter_name"] = parameter.Identifier.Text,
                            ["is_interface"] = IsInterface(paramType)
                        }
                    });
                }
            }
        }
    }

    /// <summary>
    /// Extract method calls from method body with enhanced context tracking
    /// </summary>
    private void ExtractMethodCalls(
        MethodDeclarationSyntax methodDecl, 
        string fullMethodName, 
        string context, 
        ParseResult result,
        Dictionary<string, string>? classTypeMap = null)
    {
        if (methodDecl.Body == null && methodDecl.ExpressionBody == null)
            return;

        var invocations = methodDecl.DescendantNodes().OfType<InvocationExpressionSyntax>();
        
        foreach (var invocation in invocations)
        {
            var (calledMethodName, callerObject, fullExpression) = ExtractMethodCallInfo(invocation);
            
            if (!string.IsNullOrWhiteSpace(calledMethodName))
            {
                var relationship = new CodeRelationship
                {
                    FromName = fullMethodName,
                    ToName = calledMethodName,
                    Type = RelationshipType.Calls,
                    Context = context,
                    Properties = new Dictionary<string, object>()
                };

                // Add caller object if available (e.g., "_repository" in "_repository.Save()")
                if (!string.IsNullOrWhiteSpace(callerObject))
                {
                    relationship.Properties["caller_object"] = callerObject;
                    relationship.Properties["full_expression"] = fullExpression;
                    
                    // Try to resolve the type from class type map (DI fields, constructor params, etc.)
                    if (classTypeMap != null && classTypeMap.TryGetValue(callerObject, out var inferredType))
                    {
                        relationship.Properties["inferred_type"] = inferredType;
                        
                        // Update ToName to include the type if we know it
                        // e.g., "Save" -> "IUserRepository.Save"
                        var methodNameOnly = calledMethodName.Contains('.') 
                            ? calledMethodName.Split('.').Last() 
                            : calledMethodName;
                        relationship.ToName = $"{inferredType}.{methodNameOnly}";
                    }
                }

                // Add line number for better traceability
                var lineNumber = invocation.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                relationship.Properties["line_number"] = lineNumber;

                result.Relationships.Add(relationship);
            }
        }
    }

    /// <summary>
    /// Extract method parameter types
    /// </summary>
    private void ExtractMethodParameterTypes(MethodDeclarationSyntax methodDecl, string fullMethodName, string context, ParseResult result)
    {
        foreach (var parameter in methodDecl.ParameterList.Parameters)
        {
            var paramType = parameter.Type?.ToString();
            if (!string.IsNullOrWhiteSpace(paramType) && !IsPrimitiveType(paramType))
            {
                // Extract base type (remove generic parameters for relationship)
                var baseType = ExtractBaseTypeName(paramType);
                
                result.Relationships.Add(new CodeRelationship
                {
                    FromName = fullMethodName,
                    ToName = baseType,
                    Type = RelationshipType.AcceptsType,
                    Context = context,
                    Properties = new Dictionary<string, object>
                    {
                        ["parameter_name"] = parameter.Identifier.Text,
                        ["full_type"] = paramType
                    }
                });

                // Extract generic type parameters if any
                ExtractGenericTypesFromString(paramType, fullMethodName, context, result);
            }
        }
    }

    /// <summary>
    /// Extract method return type
    /// </summary>
    private void ExtractMethodReturnType(MethodDeclarationSyntax methodDecl, string fullMethodName, string context, ParseResult result)
    {
        var returnType = methodDecl.ReturnType.ToString();
        if (!string.IsNullOrWhiteSpace(returnType) && 
            returnType != "void" && 
            !IsPrimitiveType(returnType))
        {
            var baseType = ExtractBaseTypeName(returnType);
            
            result.Relationships.Add(new CodeRelationship
            {
                FromName = fullMethodName,
                ToName = baseType,
                Type = RelationshipType.ReturnsType,
                Context = context,
                Properties = new Dictionary<string, object>
                {
                    ["full_type"] = returnType
                }
            });

            // Extract generic type parameters if any
            ExtractGenericTypesFromString(returnType, fullMethodName, context, result);
        }
    }

    /// <summary>
    /// Extract property type
    /// </summary>
    private void ExtractPropertyType(PropertyDeclarationSyntax propertyDecl, string fullPropertyName, string context, ParseResult result)
    {
        var propertyType = propertyDecl.Type.ToString();
        if (!string.IsNullOrWhiteSpace(propertyType) && !IsPrimitiveType(propertyType))
        {
            var baseType = ExtractBaseTypeName(propertyType);
            
            result.Relationships.Add(new CodeRelationship
            {
                FromName = fullPropertyName,
                ToName = baseType,
                Type = RelationshipType.HasType,
                Context = context,
                Properties = new Dictionary<string, object>
                {
                    ["full_type"] = propertyType
                }
            });

            // Extract generic type parameters if any
            ExtractGenericTypesFromString(propertyType, fullPropertyName, context, result);
        }
    }

    /// <summary>
    /// Extract attributes from attribute lists
    /// </summary>
    private void ExtractAttributes(SyntaxList<AttributeListSyntax> attributeLists, string elementName, string context, ParseResult result)
    {
        foreach (var attributeList in attributeLists)
        {
            foreach (var attribute in attributeList.Attributes)
            {
                var attributeName = attribute.Name.ToString();
                
                result.Relationships.Add(new CodeRelationship
                {
                    FromName = elementName,
                    ToName = attributeName,
                    Type = RelationshipType.HasAttribute,
                    Context = context,
                    Properties = new Dictionary<string, object>
                    {
                        ["arguments"] = attribute.ArgumentList?.Arguments.ToString() ?? ""
                    }
                });
            }
        }
    }

    /// <summary>
    /// Extract exception handling (try-catch, throws)
    /// </summary>
    private void ExtractExceptionHandling(MethodDeclarationSyntax methodDecl, string fullMethodName, string context, ParseResult result)
    {
        if (methodDecl.Body == null && methodDecl.ExpressionBody == null)
            return;

        // Extract catch clauses
        var catchClauses = methodDecl.DescendantNodes().OfType<CatchClauseSyntax>();
        foreach (var catchClause in catchClauses)
        {
            var exceptionType = catchClause.Declaration?.Type.ToString();
            if (!string.IsNullOrWhiteSpace(exceptionType))
            {
                result.Relationships.Add(new CodeRelationship
                {
                    FromName = fullMethodName,
                    ToName = exceptionType,
                    Type = RelationshipType.Catches,
                    Context = context
                });
            }
        }

        // Extract throw statements
        var throwStatements = methodDecl.DescendantNodes().OfType<ThrowStatementSyntax>();
        foreach (var throwStmt in throwStatements)
        {
            if (throwStmt.Expression is ObjectCreationExpressionSyntax creation)
            {
                var exceptionType = creation.Type.ToString();
                if (!string.IsNullOrWhiteSpace(exceptionType))
                {
                    result.Relationships.Add(new CodeRelationship
                    {
                        FromName = fullMethodName,
                        ToName = exceptionType,
                        Type = RelationshipType.Throws,
                        Context = context
                    });
                }
            }
        }
    }

    /// <summary>
    /// Extract generic type parameters from type parameter list
    /// </summary>
    private void ExtractGenericTypes(TypeParameterListSyntax? typeParameterList, string elementName, string context, ParseResult result)
    {
        if (typeParameterList == null)
            return;

        foreach (var typeParameter in typeParameterList.Parameters)
        {
            var constraintClauses = typeParameter.Parent?.Parent as ClassDeclarationSyntax;
            if (constraintClauses?.ConstraintClauses != null)
            {
                foreach (var constraint in constraintClauses.ConstraintClauses)
                {
                    if (constraint.Name.ToString() == typeParameter.Identifier.Text)
                    {
                        foreach (var typeConstraint in constraint.Constraints.OfType<TypeConstraintSyntax>())
                        {
                            var constraintType = typeConstraint.Type.ToString();
                            result.Relationships.Add(new CodeRelationship
                            {
                                FromName = elementName,
                                ToName = constraintType,
                                Type = RelationshipType.UsesGeneric,
                                Context = context,
                                Properties = new Dictionary<string, object>
                                {
                                    ["type_parameter"] = typeParameter.Identifier.Text
                                }
                            });
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Extract generic types from a type string (e.g., "List<User>" -> "User")
    /// </summary>
    private void ExtractGenericTypesFromString(string typeString, string elementName, string context, ParseResult result)
    {
        // Match generic type parameters like List<User>, Dictionary<string, Order>, etc.
        var genericMatch = System.Text.RegularExpressions.Regex.Match(typeString, @"<(.+)>");
        if (genericMatch.Success)
        {
            var genericParams = genericMatch.Groups[1].Value.Split(',');
            foreach (var param in genericParams)
            {
                var cleanParam = param.Trim();
                if (!string.IsNullOrWhiteSpace(cleanParam) && !IsPrimitiveType(cleanParam))
                {
                    result.Relationships.Add(new CodeRelationship
                    {
                        FromName = elementName,
                        ToName = cleanParam,
                        Type = RelationshipType.UsesGeneric,
                        Context = context
                    });
                }
            }
        }
    }

    /// <summary>
    /// Extract comprehensive method call information from invocation expression
    /// Returns: (methodName, callerObject, fullExpression)
    /// </summary>
    private (string methodName, string callerObject, string fullExpression) ExtractMethodCallInfo(InvocationExpressionSyntax invocation)
    {
        var fullExpression = invocation.Expression.ToString();
        
        switch (invocation.Expression)
        {
            // Simple method call: DoSomething()
            case IdentifierNameSyntax identifierName:
                return (identifierName.Identifier.Text, "", fullExpression);
            
            // Member access: _repository.Save() or user.GetName()
            case MemberAccessExpressionSyntax memberAccess:
                var methodName = memberAccess.Name.Identifier.Text;
                var callerObject = ExtractCallerObject(memberAccess.Expression);
                
                // If we have a caller object, create qualified name
                var qualifiedName = !string.IsNullOrWhiteSpace(callerObject) 
                    ? $"{callerObject}.{methodName}"
                    : methodName;
                
                return (qualifiedName, callerObject, fullExpression);
            
            // Generic method: DoSomething<T>()
            case GenericNameSyntax genericName:
                return (genericName.Identifier.Text, "", fullExpression);
            
            default:
                return (fullExpression, "", fullExpression);
        };
    }

    /// <summary>
    /// Extract the caller object from a member access expression
    /// Examples: _repository, this, _context.Users, etc.
    /// </summary>
    private string ExtractCallerObject(ExpressionSyntax expression)
    {
        return expression switch
        {
            // Field or local variable: _repository, user, etc.
            IdentifierNameSyntax identifier => identifier.Identifier.Text,
            
            // this.Method()
            ThisExpressionSyntax => "this",
            
            // base.Method()
            BaseExpressionSyntax => "base",
            
            // Nested member access: _context.Users
            MemberAccessExpressionSyntax memberAccess => memberAccess.ToString(),
            
            // Everything else
            _ => expression.ToString()
        };
    }

    /// <summary>
    /// Legacy method - redirects to ExtractMethodCallInfo for backwards compatibility
    /// </summary>
    private string ExtractMethodNameFromInvocation(InvocationExpressionSyntax invocation)
    {
        var (methodName, _, _) = ExtractMethodCallInfo(invocation);
        return methodName;
    }

    /// <summary>
    /// Extract base type name (remove generic parameters)
    /// E.g., "List<User>" -> "List", "Dictionary<string, int>" -> "Dictionary"
    /// </summary>
    private string ExtractBaseTypeName(string typeName)
    {
        var genericIndex = typeName.IndexOf('<');
        return genericIndex > 0 ? typeName.Substring(0, genericIndex) : typeName;
    }

    #region Semantic Chunking - ASP.NET Core Patterns

    /// <summary>
    /// Enhance method metadata with ASP.NET action method semantics
    /// </summary>
    private void EnhanceWithActionMethodSemantics(
        MethodDeclarationSyntax methodDecl,
        string fullClassName,
        string methodName,
        CodeMemory methodMemory,
        ParseResult result,
        string context)
    {
        var attributes = methodDecl.AttributeLists.SelectMany(al => al.Attributes).ToList();
        
        // Check if this is a controller action method
        var httpVerb = GetHttpVerb(attributes);
        if (string.IsNullOrEmpty(httpVerb))
            return; // Not an action method

        // It's an action method - enhance metadata
        methodMemory.Metadata["chunk_type"] = "action_method";
        methodMemory.Metadata["controller"] = fullClassName;
        methodMemory.Metadata["action"] = methodName;
        methodMemory.Metadata["http_method"] = httpVerb;
        
        // Extract route
        var route = ExtractRoute(attributes, fullClassName, methodName);
        if (!string.IsNullOrEmpty(route))
        {
            methodMemory.Metadata["route"] = route;
            
            // Create Endpoint node and EXPOSES relationship
            var endpointName = $"Endpoint({httpVerb} {route})";
            var endpoint = new CodeMemory
            {
                Type = CodeMemoryType.Pattern,
                Name = endpointName,
                Content = $"{httpVerb} {route}",
                FilePath = methodMemory.FilePath,
                Context = context,
                LineNumber = methodMemory.LineNumber,
                Metadata = new Dictionary<string, object>
                {
                    ["chunk_type"] = "endpoint",
                    ["route"] = route,
                    ["http_method"] = httpVerb,
                    ["controller"] = fullClassName,
                    ["action"] = methodName,
                    ["framework"] = "aspnet-core",
                    ["layer"] = "API"
                }
            };
            result.CodeElements.Add(endpoint);
            
            // EXPOSES relationship
            result.Relationships.Add(new CodeRelationship
            {
                FromName = endpointName,
                ToName = $"{fullClassName}.{methodName}",
                Type = RelationshipType.Exposes,
                Context = context,
                Properties = new Dictionary<string, object>
                {
                    ["http_method"] = httpVerb
                }
            });
        }
        
        // Extract authorization
        var authInfo = ExtractAuthorization(attributes);
        if (authInfo.hasAuth)
        {
            methodMemory.Metadata["requires_auth"] = true;
            
            if (authInfo.roles.Any())
            {
                methodMemory.Metadata["auth_roles"] = authInfo.roles;
                
                foreach (var role in authInfo.roles)
                {
                    result.Relationships.Add(new CodeRelationship
                    {
                        FromName = $"{fullClassName}.{methodName}",
                        ToName = $"Role({role})",
                        Type = RelationshipType.Authorizes,
                        Context = context
                    });
                }
            }
            
            if (!string.IsNullOrEmpty(authInfo.policy))
            {
                methodMemory.Metadata["auth_policy"] = authInfo.policy;
                
                result.Relationships.Add(new CodeRelationship
                {
                    FromName = $"{fullClassName}.{methodName}",
                    ToName = $"Policy({authInfo.policy})",
                    Type = RelationshipType.RequiresPolicy,
                    Context = context
                });
            }
        }
        
        // Extract request/response DTOs from parameters and return type
        var requestDtos = methodDecl.ParameterList.Parameters
            .Select(p => p.Type?.ToString())
            .Where(t => !string.IsNullOrWhiteSpace(t) && t.EndsWith("Dto", StringComparison.OrdinalIgnoreCase))
            .ToList();
        
        if (requestDtos.Any())
        {
            methodMemory.Metadata["request_dtos"] = requestDtos;
        }
        
        var returnType = ExtractReturnTypeFromTask(methodDecl.ReturnType.ToString());
        if (returnType.EndsWith("Dto", StringComparison.OrdinalIgnoreCase))
        {
            methodMemory.Metadata["response_dto"] = returnType;
        }
        
        // Check for ModelState validation
        var hasModelValidation = methodDecl.DescendantNodes()
            .OfType<IdentifierNameSyntax>()
            .Any(i => i.Identifier.Text == "ModelState");
        
        if (hasModelValidation)
        {
            methodMemory.Metadata["validates_model"] = true;
        }
        
        // Add layer and framework
        methodMemory.Metadata["framework"] = "aspnet-core";
        methodMemory.Metadata["layer"] = "API";
    }

    /// <summary>
    /// Extract HTTP verb from method attributes
    /// </summary>
    private string GetHttpVerb(List<AttributeSyntax> attributes)
    {
        foreach (var attr in attributes)
        {
            var attrName = attr.Name.ToString();
            if (attrName.StartsWith("Http"))
            {
                return attrName switch
                {
                    "HttpGet" => "GET",
                    "HttpPost" => "POST",
                    "HttpPut" => "PUT",
                    "HttpDelete" => "DELETE",
                    "HttpPatch" => "PATCH",
                    "HttpHead" => "HEAD",
                    "HttpOptions" => "OPTIONS",
                    _ => attrName.Replace("Http", "").ToUpperInvariant()
                };
            }
        }
        return string.Empty;
    }

    /// <summary>
    /// Extract route from method and class attributes
    /// </summary>
    private string ExtractRoute(List<AttributeSyntax> methodAttributes, string className, string methodName)
    {
        var baseRoute = "/api"; // Default
        
        // Try to get route from class name (e.g., UsersController -> /api/users)
        if (className.EndsWith("Controller"))
        {
            var controllerName = className.Substring(0, className.Length - "Controller".Length);
            baseRoute = $"/api/{controllerName.ToLowerInvariant()}";
        }
        
        // Check for explicit route on method
        foreach (var attr in methodAttributes)
        {
            var attrName = attr.Name.ToString();
            
            // HttpGet("{id}"), Route("api/users/{id}"), etc.
            if (attrName.StartsWith("Http") || attrName == "Route")
            {
                var args = attr.ArgumentList?.Arguments;
                if (args != null && args.Value.Count > 0)
                {
                    var routeArg = args.Value[0].ToString().Trim('"');
                    
                    if (routeArg.StartsWith("/") || routeArg.StartsWith("api/"))
                    {
                        return "/" + routeArg.TrimStart('/');
                    }
                    else
                    {
                        return $"{baseRoute}/{routeArg}";
                    }
                }
            }
        }
        
        return baseRoute;
    }

    /// <summary>
    /// Extract authorization info from attributes
    /// </summary>
    private (bool hasAuth, List<string> roles, string policy) ExtractAuthorization(List<AttributeSyntax> attributes)
    {
        var hasAuth = false;
        var roles = new List<string>();
        string policy = string.Empty;
        
        foreach (var attr in attributes)
        {
            var attrName = attr.Name.ToString();
            
            if (attrName == "Authorize")
            {
                hasAuth = true;
                
                if (attr.ArgumentList != null)
                {
                    foreach (var arg in attr.ArgumentList.Arguments)
                    {
                        var argStr = arg.ToString();
                        
                        // Roles = "Admin,Manager"
                        if (argStr.Contains("Roles"))
                        {
                            var rolesMatch = System.Text.RegularExpressions.Regex.Match(argStr, @"Roles\s*=\s*""([^""]+)""");
                            if (rolesMatch.Success)
                            {
                                roles.AddRange(rolesMatch.Groups[1].Value.Split(',').Select(r => r.Trim()));
                            }
                        }
                        
                        // Policy = "RequireAdmin"
                        if (argStr.Contains("Policy"))
                        {
                            var policyMatch = System.Text.RegularExpressions.Regex.Match(argStr, @"Policy\s*=\s*""([^""]+)""");
                            if (policyMatch.Success)
                            {
                                policy = policyMatch.Groups[1].Value;
                            }
                        }
                    }
                }
            }
        }
        
        return (hasAuth, roles, policy);
    }

    /// <summary>
    /// Enhance method with EF query semantics
    /// </summary>
    private void EnhanceWithEFQuerySemantics(
        MethodDeclarationSyntax methodDecl,
        string fullMethodName,
        CodeMemory methodMemory,
        ParseResult result,
        string context)
    {
        if (methodDecl.Body == null && methodDecl.ExpressionBody == null)
            return;
        
        var allNodes = methodDecl.Body?.DescendantNodes() ?? 
                      methodDecl.ExpressionBody?.DescendantNodes() ?? 
                      Enumerable.Empty<SyntaxNode>();
        
        var invocations = allNodes.OfType<InvocationExpressionSyntax>().ToList();
        
        // Detect EF query operations
        var efOperations = new List<string>();
        var includedEntities = new List<string>();
        var queriedEntity = string.Empty;
        var projectionDto = string.Empty;
        var groupByField = string.Empty;
        
        foreach (var invocation in invocations)
        {
            var methodNameInvoked = ExtractMethodNameFromInvocation(invocation);
            
            // EF query operations
            if (IsEFQueryOperation(methodNameInvoked))
            {
                efOperations.Add(methodNameInvoked);
                
                // Extract Include/ThenInclude
                if (methodNameInvoked == "Include" || methodNameInvoked == "ThenInclude")
                {
                    var includedEntity = ExtractIncludedEntity(invocation);
                    if (!string.IsNullOrEmpty(includedEntity))
                    {
                        includedEntities.Add(includedEntity);
                    }
                }
                
                // Extract GroupBy field
                if (methodNameInvoked == "GroupBy")
                {
                    groupByField = ExtractGroupByField(invocation);
                }
            }
            
            // Detect DbSet access (e.g., db.Users, _context.Projects)
            if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
            {
                var memberName = memberAccess.Name.ToString();
                if (char.IsUpper(memberName[0]) && !memberName.Contains("Async"))
                {
                    // Likely a DbSet property (e.g., Users, Projects)
                    queriedEntity = memberName;
                }
            }
        }
        
        // If this looks like an EF query, enhance metadata
        if (efOperations.Any() && !string.IsNullOrEmpty(queriedEntity))
        {
            methodMemory.Metadata["chunk_type"] = "ef_query";
            methodMemory.Metadata["primary_entity"] = queriedEntity;
            methodMemory.Metadata["query_operations"] = efOperations;
            methodMemory.Metadata["query_complexity"] = DetermineQueryComplexity(efOperations.Count);
            methodMemory.Metadata["framework"] = "ef-core";
            methodMemory.Metadata["layer"] = "Data";
            
            if (includedEntities.Any())
            {
                methodMemory.Metadata["included_entities"] = includedEntities;
            }
            
            if (!string.IsNullOrEmpty(groupByField))
            {
                methodMemory.Metadata["groups_by"] = groupByField;
            }
            
            // Create QUERIES relationship
            result.Relationships.Add(new CodeRelationship
            {
                FromName = fullMethodName,
                ToName = $"{queriedEntity} (Entity)",
                Type = RelationshipType.Queries,
                Context = context,
                Properties = new Dictionary<string, object>
                {
                    ["operation"] = "Read"
                }
            });
            
            // Create INCLUDES relationships
            foreach (var included in includedEntities)
            {
                result.Relationships.Add(new CodeRelationship
                {
                    FromName = fullMethodName,
                    ToName = $"{included} (Entity)",
                    Type = RelationshipType.Includes,
                    Context = context,
                    Properties = new Dictionary<string, object>
                    {
                        ["eager_load"] = true
                    }
                });
            }
            
            // Create GROUPSBY relationship
            if (!string.IsNullOrEmpty(groupByField))
            {
                result.Relationships.Add(new CodeRelationship
                {
                    FromName = fullMethodName,
                    ToName = groupByField,
                    Type = RelationshipType.GroupsBy,
                    Context = context
                });
            }
            
            // Detect projection to DTO
            var selectNodes = allNodes.OfType<InvocationExpressionSyntax>()
                .Where(i => ExtractMethodNameFromInvocation(i) == "Select")
                .ToList();
            
            foreach (var select in selectNodes)
            {
                var dtoType = ExtractProjectionDto(select);
                if (!string.IsNullOrEmpty(dtoType))
                {
                    methodMemory.Metadata["projection_dto"] = dtoType;
                    
                    result.Relationships.Add(new CodeRelationship
                    {
                        FromName = fullMethodName,
                        ToName = dtoType,
                        Type = RelationshipType.Projects,
                        Context = context,
                        Properties = new Dictionary<string, object>
                        {
                            ["projection_type"] = "Select"
                        }
                    });
                }
            }
        }
    }

    /// <summary>
    /// Check if method name is an EF query operation
    /// </summary>
    private bool IsEFQueryOperation(string methodName)
    {
        var efOperations = new[]
        {
            "Where", "Select", "OrderBy", "OrderByDescending", "ThenBy", "ThenByDescending",
            "GroupBy", "Join", "GroupJoin", "SelectMany", "Distinct", "Union", "Intersect",
            "Except", "Skip", "Take", "First", "FirstOrDefault", "Single", "SingleOrDefault",
            "Last", "LastOrDefault", "Any", "All", "Count", "Sum", "Average", "Min", "Max",
            "Include", "ThenInclude", "AsNoTracking", "ToList", "ToListAsync", "ToArray",
            "ToArrayAsync", "ToDictionary", "ToDictionaryAsync", "FirstAsync", "FirstOrDefaultAsync",
            "SingleAsync", "SingleOrDefaultAsync", "CountAsync", "AnyAsync", "AllAsync"
        };
        
        return efOperations.Contains(methodName);
    }

    /// <summary>
    /// Extract included entity from Include/ThenInclude
    /// </summary>
    private string ExtractIncludedEntity(InvocationExpressionSyntax invocation)
    {
        // Include(u => u.Profile) -> Profile
        var args = invocation.ArgumentList?.Arguments;
        if (args != null && args.Value.Count > 0)
        {
            var lambdaStr = args.Value[0].ToString();
            var match = System.Text.RegularExpressions.Regex.Match(lambdaStr, @"\w+\s*=>\s*\w+\.(\w+)");
            if (match.Success)
            {
                return match.Groups[1].Value;
            }
        }
        return string.Empty;
    }

    /// <summary>
    /// Extract GroupBy field
    /// </summary>
    private string ExtractGroupByField(InvocationExpressionSyntax invocation)
    {
        var args = invocation.ArgumentList?.Arguments;
        if (args != null && args.Value.Count > 0)
        {
            var lambdaStr = args.Value[0].ToString();
            var match = System.Text.RegularExpressions.Regex.Match(lambdaStr, @"\w+\s*=>\s*\w+\.(\w+)");
            if (match.Success)
            {
                return match.Groups[1].Value;
            }
        }
        return string.Empty;
    }

    /// <summary>
    /// Extract projection DTO type from Select
    /// </summary>
    private string ExtractProjectionDto(InvocationExpressionSyntax invocation)
    {
        // Select(x => new UserDto { ... }) -> UserDto
        var args = invocation.ArgumentList?.Arguments;
        if (args != null && args.Value.Count > 0)
        {
            var lambdaStr = args.Value[0].ToString();
            var match = System.Text.RegularExpressions.Regex.Match(lambdaStr, @"new\s+(\w+Dto)");
            if (match.Success)
            {
                return match.Groups[1].Value;
            }
        }
        return string.Empty;
    }

    /// <summary>
    /// Determine query complexity based on number of operations
    /// </summary>
    private string DetermineQueryComplexity(int operationCount)
    {
        return operationCount switch
        {
            <= 3 => "low",
            <= 6 => "medium",
            _ => "high"
        };
    }

    /// <summary>
    /// Extract actual return type from Task<T> or IActionResult
    /// </summary>
    private string ExtractReturnTypeFromTask(string returnType)
    {
        // Task<UserDto> -> UserDto
        // Task<IActionResult> -> IActionResult
        var match = System.Text.RegularExpressions.Regex.Match(returnType, @"Task<(.+)>");
        if (match.Success)
        {
            return match.Groups[1].Value;
        }
        return returnType;
    }

    /// <summary>
    /// Determine code layer based on class name and namespace
    /// </summary>
    private string DetermineLayer(string className, string namespaceName)
    {
        if (className.EndsWith("Controller")) return "API";
        if (className.EndsWith("Service") || className.EndsWith("Handler")) return "Domain";
        if (className.EndsWith("Repository") || className.Contains("DbContext")) return "Data";
        if (className.EndsWith("Validator")) return "Domain";
        if (className.EndsWith("Filter") || className.EndsWith("Middleware")) return "Infra";
        if (className.EndsWith("ViewModel") || className.EndsWith("Component")) return "UI";
        if (className.Contains("Test")) return "Test";
        
        // Check namespace
        if (namespaceName.Contains(".Controllers")) return "API";
        if (namespaceName.Contains(".Services") || namespaceName.Contains(".Domain")) return "Domain";
        if (namespaceName.Contains(".Repositories") || namespaceName.Contains(".Data")) return "Data";
        if (namespaceName.Contains(".Infrastructure")) return "Infra";
        if (namespaceName.Contains(".UI") || namespaceName.Contains(".Views")) return "UI";
        if (namespaceName.Contains(".Tests")) return "Test";
        
        return "Domain"; // Default
    }

    /// <summary>
    /// Determine bounded context from namespace
    /// E.g., MyApp.UserManagement.Controllers -> UserManagement
    /// </summary>
    private string DetermineBoundedContext(string namespaceName)
    {
        var parts = namespaceName.Split('.');
        
        // Typically: AppName.BoundedContext.Layer
        // e.g., DataPrepPlatform.UserManagement.Controllers
        if (parts.Length >= 2)
        {
            // Skip first part (app name), return second part
            var secondPart = parts[1];
            
            // Filter out common layer names
            var layerNames = new[] { "Controllers", "Services", "Domain", "Data", "Infrastructure", "UI", "Views", "Models", "Repositories" };
            if (!layerNames.Contains(secondPart))
            {
                return secondPart;
            }
        }
        
        return parts.FirstOrDefault() ?? "Default";
    }

    /// <summary>
    /// Extract DI registrations from Program.cs or Startup.cs
    /// Detects services.Add* patterns
    /// </summary>
    private void ExtractDIRegistrations(
        MethodDeclarationSyntax methodDecl,
        string filePath,
        string context,
        ParseResult result)
    {
        // Only process if this looks like a configuration method (Program.cs/Startup.cs)
        var fileName = Path.GetFileName(filePath);
        if (!fileName.Contains("Program") && !fileName.Contains("Startup"))
            return;
        
        if (methodDecl.Body == null)
            return;
        
        var invocations = methodDecl.Body.DescendantNodes().OfType<InvocationExpressionSyntax>();
        
        foreach (var invocation in invocations)
        {
            var methodName = ExtractMethodNameFromInvocation(invocation);
            
            // Detect DI registration methods
            if (IsDIRegistrationMethod(methodName))
            {
                var (interfaceType, implementationType, lifetime) = ExtractDIRegistrationInfo(invocation, methodName);
                
                if (!string.IsNullOrEmpty(interfaceType))
                {
                    var lineNumber = invocation.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                    
                    // Create a code chunk for this DI registration
                    var registrationChunk = new CodeMemory
                    {
                        Type = CodeMemoryType.Pattern,
                        Name = $"DI: {interfaceType}",
                        Content = invocation.ToString(),
                        FilePath = filePath,
                        Context = context,
                        LineNumber = lineNumber,
                        Metadata = new Dictionary<string, object>
                        {
                            ["chunk_type"] = "di_registration",
                            ["interface"] = interfaceType,
                            ["implementation"] = implementationType ?? interfaceType,
                            ["lifetime"] = lifetime,
                            ["framework"] = "aspnet-core",
                            ["layer"] = "Infra"
                        }
                    };
                    result.CodeElements.Add(registrationChunk);
                    
                    // Create REGISTERS relationship
                    result.Relationships.Add(new CodeRelationship
                    {
                        FromName = "Program",
                        ToName = interfaceType,
                        Type = RelationshipType.Registers,
                        Context = context,
                        Properties = new Dictionary<string, object>
                        {
                            ["lifetime"] = lifetime
                        }
                    });
                    
                    // Create IMPLEMENTS_REGISTRATION relationship if we have both interface and implementation
                    if (!string.IsNullOrEmpty(implementationType) && interfaceType != implementationType)
                    {
                        result.Relationships.Add(new CodeRelationship
                        {
                            FromName = interfaceType,
                            ToName = implementationType,
                            Type = RelationshipType.ImplementsRegistration,
                            Context = context,
                            Properties = new Dictionary<string, object>
                            {
                                ["lifetime"] = lifetime
                            }
                        });
                    }
                }
            }
        }
    }

    /// <summary>
    /// Check if method is a DI registration method
    /// </summary>
    private bool IsDIRegistrationMethod(string methodName)
    {
        var registrationMethods = new[]
        {
            "AddScoped", "AddSingleton", "AddTransient",
            "AddDbContext", "AddHttpClient", "AddAuthentication",
            "AddAuthorization", "AddControllers", "AddRazorPages",
            "AddMvc", "AddLogging", "AddOptions", "AddMemoryCache",
            "AddDistributedMemoryCache", "AddStackExchangeRedisCache",
            "AddCors", "AddSwaggerGen", "AddHealthChecks",
            "AddHangfire", "AddMediatR", "AddAutoMapper", "AddFluentValidation"
        };
        
        return registrationMethods.Contains(methodName);
    }

    /// <summary>
    /// Extract DI registration info (interface, implementation, lifetime)
    /// </summary>
    private (string interfaceType, string implementationType, string lifetime) ExtractDIRegistrationInfo(
        InvocationExpressionSyntax invocation,
        string methodName)
    {
        string lifetime = methodName switch
        {
            "AddScoped" => "Scoped",
            "AddSingleton" => "Singleton",
            "AddTransient" => "Transient",
            "AddDbContext" => "Scoped", // DbContext is typically scoped
            "AddHttpClient" => "Transient", // HttpClient is typically transient
            _ => "Singleton" // Default for infrastructure services
        };
        
        // Try to extract generic type parameters: AddScoped<IUserService, UserService>()
        if (invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
            memberAccess.Name is GenericNameSyntax genericName)
        {
            var typeArgs = genericName.TypeArgumentList.Arguments;
            
            if (typeArgs.Count == 2)
            {
                // Interface and implementation
                return (typeArgs[0].ToString(), typeArgs[1].ToString(), lifetime);
            }
            else if (typeArgs.Count == 1)
            {
                // Single type (concrete class or self-registration)
                return (typeArgs[0].ToString(), typeArgs[0].ToString(), lifetime);
            }
        }
        
        // For non-generic registrations, try to extract from arguments
        // AddDbContext(options => ...) - we won't get type info this way, so skip
        
        return (string.Empty, string.Empty, lifetime);
    }

    /// <summary>
    /// Extract validation logic from classes
    /// Detects DataAnnotations and FluentValidation
    /// </summary>
    private void ExtractValidationLogic(
        ClassDeclarationSyntax classDecl,
        string fullClassName,
        string filePath,
        string context,
        ParseResult result)
    {
        // Check if this is a FluentValidation validator
        var isFluentValidator = classDecl.BaseList?.Types
            .Any(t => t.Type.ToString().Contains("AbstractValidator")) ?? false;
        
        if (isFluentValidator)
        {
            ExtractFluentValidation(classDecl, fullClassName, filePath, context, result);
        }
        
        // Check for DataAnnotations on properties
        var properties = classDecl.Members.OfType<PropertyDeclarationSyntax>();
        var hasDataAnnotations = properties.Any(p => p.AttributeLists.Any());
        
        if (hasDataAnnotations)
        {
            ExtractDataAnnotationValidation(classDecl, fullClassName, filePath, context, result);
        }
    }

    /// <summary>
    /// Extract FluentValidation rules
    /// </summary>
    private void ExtractFluentValidation(
        ClassDeclarationSyntax classDecl,
        string fullClassName,
        string filePath,
        string context,
        ParseResult result)
    {
        // Get the validated type from AbstractValidator<T>
        var baseType = classDecl.BaseList?.Types.FirstOrDefault()?.Type.ToString();
        var match = System.Text.RegularExpressions.Regex.Match(baseType ?? "", @"AbstractValidator<(\w+)>");
        if (!match.Success)
            return;
        
        var validatedType = match.Groups[1].Value;
        var lineNumber = classDecl.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
        
        // Create validation chunk
        var validationChunk = new CodeMemory
        {
            Type = CodeMemoryType.Pattern,
            Name = $"Validation: {validatedType}",
            Content = classDecl.ToString(),
            FilePath = filePath,
            Context = context,
            LineNumber = lineNumber,
            Metadata = new Dictionary<string, object>
            {
                ["chunk_type"] = "validation",
                ["validator_name"] = fullClassName,
                ["validated_type"] = validatedType,
                ["validation_framework"] = "FluentValidation",
                ["framework"] = "fluentvalidation",
                ["layer"] = "Domain"
            }
        };
        
        // Extract RuleFor calls to get validated properties
        var ruleForCalls = classDecl.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(i => ExtractMethodNameFromInvocation(i) == "RuleFor")
            .ToList();
        
        var validatedProperties = new List<string>();
        foreach (var ruleFor in ruleForCalls)
        {
            var property = ExtractRuleForProperty(ruleFor);
            if (!string.IsNullOrEmpty(property))
            {
                validatedProperties.Add(property);
            }
        }
        
        if (validatedProperties.Any())
        {
            validationChunk.Metadata["properties_validated"] = validatedProperties;
        }
        
        result.CodeElements.Add(validationChunk);
        
        // Create VALIDATES relationship
        result.Relationships.Add(new CodeRelationship
        {
            FromName = fullClassName,
            ToName = validatedType,
            Type = RelationshipType.Validates,
            Context = context,
            Properties = new Dictionary<string, object>
            {
                ["framework"] = "FluentValidation"
            }
        });
    }

    /// <summary>
    /// Extract property from RuleFor(x => x.PropertyName)
    /// </summary>
    private string ExtractRuleForProperty(InvocationExpressionSyntax invocation)
    {
        var args = invocation.ArgumentList?.Arguments;
        if (args != null && args.Value.Count > 0)
        {
            var lambdaStr = args.Value[0].ToString();
            var match = System.Text.RegularExpressions.Regex.Match(lambdaStr, @"\w+\s*=>\s*\w+\.(\w+)");
            if (match.Success)
            {
                return match.Groups[1].Value;
            }
        }
        return string.Empty;
    }

    /// <summary>
    /// Extract DataAnnotation validation from class properties
    /// </summary>
    private void ExtractDataAnnotationValidation(
        ClassDeclarationSyntax classDecl,
        string fullClassName,
        string filePath,
        string context,
        ParseResult result)
    {
        var properties = classDecl.Members.OfType<PropertyDeclarationSyntax>();
        var validationRules = new Dictionary<string, List<string>>();
        
        foreach (var property in properties)
        {
            var propertyName = property.Identifier.Text;
            var rules = new List<string>();
            
            foreach (var attributeList in property.AttributeLists)
            {
                foreach (var attribute in attributeList.Attributes)
                {
                    var attrName = attribute.Name.ToString();
                    
                    // Common DataAnnotation attributes
                    if (IsValidationAttribute(attrName))
                    {
                        rules.Add(attrName);
                    }
                }
            }
            
            if (rules.Any())
            {
                validationRules[propertyName] = rules;
            }
        }
        
        if (!validationRules.Any())
            return;
        
        var lineNumber = classDecl.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
        
        // Create validation chunk
        var validationChunk = new CodeMemory
        {
            Type = CodeMemoryType.Pattern,
            Name = $"Validation: {fullClassName}",
            Content = string.Join("\n", properties.Where(p => validationRules.ContainsKey(p.Identifier.Text)).Select(p => p.ToString())),
            FilePath = filePath,
            Context = context,
            LineNumber = lineNumber,
            Metadata = new Dictionary<string, object>
            {
                ["chunk_type"] = "validation",
                ["model"] = fullClassName,
                ["validation_framework"] = "DataAnnotations",
                ["framework"] = "aspnet-core",
                ["layer"] = "Domain",
                ["properties_validated"] = validationRules.Keys.ToList(),
                ["validation_rules"] = validationRules
            }
        };
        result.CodeElements.Add(validationChunk);
        
        // Create VALIDATES relationship (from model to itself for DataAnnotations)
        result.Relationships.Add(new CodeRelationship
        {
            FromName = $"DataAnnotations({fullClassName})",
            ToName = fullClassName,
            Type = RelationshipType.Validates,
            Context = context,
            Properties = new Dictionary<string, object>
            {
                ["framework"] = "DataAnnotations"
            }
        });
    }

    /// <summary>
    /// Check if attribute is a validation attribute
    /// </summary>
    private bool IsValidationAttribute(string attributeName)
    {
        var validationAttributes = new[]
        {
            "Required", "MaxLength", "MinLength", "StringLength", "Range",
            "RegularExpression", "EmailAddress", "Phone", "Url", "CreditCard",
            "Compare", "DataType", "DisplayFormat", "DisplayName", "EnumDataType",
            "FileExtensions", "MaxLengthAttribute", "MinLengthAttribute"
        };
        
        return validationAttributes.Contains(attributeName);
    }

    #endregion

    #region Phase 3 - Advanced Patterns

    /// <summary>
    /// Extract middleware pipeline from Program.cs
    /// Tracks app.Use* method calls and their order
    /// </summary>
    private void ExtractMiddlewarePipeline(
        MethodDeclarationSyntax methodDecl,
        string filePath,
        string context,
        ParseResult result)
    {
        var fileName = Path.GetFileName(filePath);
        if (!fileName.Contains("Program") && !fileName.Contains("Startup"))
            return;
        
        if (methodDecl.Body == null)
            return;
        
        var invocations = methodDecl.Body.DescendantNodes().OfType<InvocationExpressionSyntax>().ToList();
        var middlewareStack = new List<(string name, int order, int lineNumber)>();
        int order = 1;
        
        foreach (var invocation in invocations)
        {
            var methodName = ExtractMethodNameFromInvocation(invocation);
            
            if (IsMiddlewareMethod(methodName))
            {
                var lineNumber = invocation.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                middlewareStack.Add((methodName, order++, lineNumber));
            }
        }
        
        if (middlewareStack.Any())
        {
            // Create middleware pipeline chunk
            var pipelineChunk = new CodeMemory
            {
                Type = CodeMemoryType.Pattern,
                Name = "MiddlewarePipeline",
                Content = string.Join("\n", middlewareStack.Select(m => $"{m.order}. {m.name}")),
                FilePath = filePath,
                Context = context,
                LineNumber = middlewareStack.First().lineNumber,
                Metadata = new Dictionary<string, object>
                {
                    ["chunk_type"] = "middleware_pipeline",
                    ["middleware_stack"] = middlewareStack.Select(m => m.name).ToList(),
                    ["middleware_count"] = middlewareStack.Count,
                    ["framework"] = "aspnet-core",
                    ["layer"] = "Infra"
                }
            };
            result.CodeElements.Add(pipelineChunk);
            
            // Create relationships for middleware execution order
            for (int i = 0; i < middlewareStack.Count; i++)
            {
                var middleware = middlewareStack[i];
                
                result.Relationships.Add(new CodeRelationship
                {
                    FromName = "MiddlewarePipeline",
                    ToName = middleware.name,
                    Type = RelationshipType.UsesMiddleware,
                    Context = context,
                    Properties = new Dictionary<string, object>
                    {
                        ["order"] = middleware.order
                    }
                });
                
                // Create PRECEDES relationship between consecutive middleware
                if (i < middlewareStack.Count - 1)
                {
                    result.Relationships.Add(new CodeRelationship
                    {
                        FromName = middleware.name,
                        ToName = middlewareStack[i + 1].name,
                        Type = RelationshipType.Precedes,
                        Context = context
                    });
                }
            }
        }
    }

    private bool IsMiddlewareMethod(string methodName)
    {
        var middlewareMethods = new[]
        {
            "UseHttpsRedirection", "UseStaticFiles", "UseRouting", "UseCors",
            "UseAuthentication", "UseAuthorization", "UseSession", "UseResponseCaching",
            "UseResponseCompression", "UseHsts", "UseForwardedHeaders", "UseRateLimiter",
            "UseEndpoints", "MapControllers", "MapRazorPages", "MapHub", "MapFallback",
            "UseExceptionHandler", "UseDeveloperExceptionPage", "UseStatusCodePages"
        };
        
        return middlewareMethods.Contains(methodName);
    }

    /// <summary>
    /// Extract background job definitions (Hangfire, IHostedService)
    /// </summary>
    private void ExtractBackgroundJobs(
        ClassDeclarationSyntax classDecl,
        string fullClassName,
        string filePath,
        string context,
        ParseResult result)
    {
        // Check for IHostedService or BackgroundService
        var implementsHostedService = classDecl.BaseList?.Types
            .Any(t => t.Type.ToString().Contains("IHostedService") || 
                     t.Type.ToString().Contains("BackgroundService")) ?? false;
        
        if (implementsHostedService)
        {
            ExtractHostedService(classDecl, fullClassName, filePath, context, result);
        }
        
        // Check for Hangfire job attributes
        var methods = classDecl.Members.OfType<MethodDeclarationSyntax>();
        foreach (var method in methods)
        {
            ExtractHangfireJob(method, fullClassName, filePath, context, result);
        }
    }

    private void ExtractHostedService(
        ClassDeclarationSyntax classDecl,
        string fullClassName,
        string filePath,
        string context,
        ParseResult result)
    {
        var lineNumber = classDecl.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
        
        var jobChunk = new CodeMemory
        {
            Type = CodeMemoryType.Pattern,
            Name = $"BackgroundService: {fullClassName}",
            Content = classDecl.ToString(),
            FilePath = filePath,
            Context = context,
            LineNumber = lineNumber,
            Metadata = new Dictionary<string, object>
            {
                ["chunk_type"] = "background_service",
                ["service_name"] = fullClassName,
                ["framework"] = "aspnet-core",
                ["layer"] = "Infra",
                ["job_type"] = "IHostedService"
            }
        };
        result.CodeElements.Add(jobChunk);
        
        result.Relationships.Add(new CodeRelationship
        {
            FromName = "Program",
            ToName = fullClassName,
            Type = RelationshipType.Schedules,
            Context = context,
            Properties = new Dictionary<string, object>
            {
                ["job_type"] = "HostedService"
            }
        });
    }

    private void ExtractHangfireJob(
        MethodDeclarationSyntax methodDecl,
        string fullClassName,
        string filePath,
        string context,
        ParseResult result)
    {
        var attributes = methodDecl.AttributeLists.SelectMany(al => al.Attributes).ToList();
        var isHangfireJob = attributes.Any(a => 
            a.Name.ToString().Contains("AutomaticRetry") ||
            a.Name.ToString().Contains("DisableConcurrentExecution") ||
            a.Name.ToString().Contains("Queue"));
        
        if (isHangfireJob)
        {
            var methodName = methodDecl.Identifier.Text;
            var lineNumber = methodDecl.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
            
            var jobChunk = new CodeMemory
            {
                Type = CodeMemoryType.Pattern,
                Name = $"HangfireJob: {fullClassName}.{methodName}",
                Content = methodDecl.ToString(),
                FilePath = filePath,
                Context = context,
                LineNumber = lineNumber,
                Metadata = new Dictionary<string, object>
                {
                    ["chunk_type"] = "hangfire_job",
                    ["job_name"] = $"{fullClassName}.{methodName}",
                    ["framework"] = "hangfire",
                    ["layer"] = "Infra"
                }
            };
            result.CodeElements.Add(jobChunk);
            
            result.Relationships.Add(new CodeRelationship
            {
                FromName = "Hangfire",
                ToName = $"{fullClassName}.{methodName}",
                Type = RelationshipType.Schedules,
                Context = context,
                Properties = new Dictionary<string, object>
                {
                    ["job_type"] = "Hangfire"
                }
            });
        }
    }

    /// <summary>
    /// Extract MediatR handlers (Commands, Queries, Events)
    /// </summary>
    private void ExtractMediatRHandlers(
        ClassDeclarationSyntax classDecl,
        string fullClassName,
        string filePath,
        string context,
        ParseResult result)
    {
        var baseTypes = classDecl.BaseList?.Types.Select(t => t.Type.ToString()).ToList() ?? new List<string>();
        
        foreach (var baseType in baseTypes)
        {
            // IRequestHandler<TRequest, TResponse>
            var requestHandlerMatch = System.Text.RegularExpressions.Regex.Match(baseType, @"IRequestHandler<([^,]+),\s*([^>]+)>");
            if (requestHandlerMatch.Success)
            {
                var requestType = requestHandlerMatch.Groups[1].Value.Trim();
                var responseType = requestHandlerMatch.Groups[2].Value.Trim();
                var messageType = DetermineMessageType(requestType);
                
                var lineNumber = classDecl.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                
                var handlerChunk = new CodeMemory
                {
                    Type = CodeMemoryType.Pattern,
                    Name = $"Handler: {fullClassName}",
                    Content = classDecl.ToString(),
                    FilePath = filePath,
                    Context = context,
                    LineNumber = lineNumber,
                    Metadata = new Dictionary<string, object>
                    {
                        ["chunk_type"] = "mediatr_handler",
                        ["handler"] = fullClassName,
                        ["message"] = requestType,
                        ["message_type"] = messageType,
                        ["return_type"] = responseType,
                        ["framework"] = "mediatr",
                        ["layer"] = "Domain",
                        ["pattern"] = "CQRS"
                    }
                };
                result.CodeElements.Add(handlerChunk);
                
                result.Relationships.Add(new CodeRelationship
                {
                    FromName = fullClassName,
                    ToName = requestType,
                    Type = RelationshipType.Handles,
                    Context = context,
                    Properties = new Dictionary<string, object>
                    {
                        ["message_type"] = messageType,
                        ["response_type"] = responseType
                    }
                });
            }
            
            // INotificationHandler<TNotification>
            var notificationHandlerMatch = System.Text.RegularExpressions.Regex.Match(baseType, @"INotificationHandler<([^>]+)>");
            if (notificationHandlerMatch.Success)
            {
                var notificationType = notificationHandlerMatch.Groups[1].Value.Trim();
                
                result.Relationships.Add(new CodeRelationship
                {
                    FromName = fullClassName,
                    ToName = notificationType,
                    Type = RelationshipType.Handles,
                    Context = context,
                    Properties = new Dictionary<string, object>
                    {
                        ["message_type"] = "Event"
                    }
                });
            }
        }
    }

    private string DetermineMessageType(string requestType)
    {
        if (requestType.Contains("Command")) return "Command";
        if (requestType.Contains("Query")) return "Query";
        if (requestType.Contains("Event")) return "Event";
        return "Request";
    }

    /// <summary>
    /// Extract AutoMapper profiles
    /// </summary>
    private void ExtractAutoMapperProfiles(
        ClassDeclarationSyntax classDecl,
        string fullClassName,
        string filePath,
        string context,
        ParseResult result)
    {
        var inheritsProfile = classDecl.BaseList?.Types
            .Any(t => t.Type.ToString() == "Profile") ?? false;
        
        if (!inheritsProfile)
            return;
        
        var lineNumber = classDecl.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
        
        var profileChunk = new CodeMemory
        {
            Type = CodeMemoryType.Pattern,
            Name = $"AutoMapper: {fullClassName}",
            Content = classDecl.ToString(),
            FilePath = filePath,
            Context = context,
            LineNumber = lineNumber,
            Metadata = new Dictionary<string, object>
            {
                ["chunk_type"] = "automapper_profile",
                ["profile_name"] = fullClassName,
                ["framework"] = "automapper",
                ["layer"] = "Domain"
            }
        };
        result.CodeElements.Add(profileChunk);
        
        // Extract CreateMap calls
        var createMapCalls = classDecl.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(i => ExtractMethodNameFromInvocation(i) == "CreateMap")
            .ToList();
        
        foreach (var createMap in createMapCalls)
        {
            var (sourceType, destType) = ExtractCreateMapTypes(createMap);
            if (!string.IsNullOrEmpty(sourceType) && !string.IsNullOrEmpty(destType))
            {
                result.Relationships.Add(new CodeRelationship
                {
                    FromName = sourceType,
                    ToName = destType,
                    Type = RelationshipType.Projects,
                    Context = context,
                    Properties = new Dictionary<string, object>
                    {
                        ["mapper"] = "AutoMapper",
                        ["profile"] = fullClassName
                    }
                });
            }
        }
    }

    private (string sourceType, string destType) ExtractCreateMapTypes(InvocationExpressionSyntax invocation)
    {
        if (invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
            memberAccess.Name is GenericNameSyntax genericName)
        {
            var typeArgs = genericName.TypeArgumentList.Arguments;
            if (typeArgs.Count == 2)
            {
                return (typeArgs[0].ToString(), typeArgs[1].ToString());
            }
        }
        return (string.Empty, string.Empty);
    }

    /// <summary>
    /// Extract authorization policy definitions
    /// </summary>
    private void ExtractAuthorizationPolicies(
        MethodDeclarationSyntax methodDecl,
        string filePath,
        string context,
        ParseResult result)
    {
        var fileName = Path.GetFileName(filePath);
        if (!fileName.Contains("Program") && !fileName.Contains("Startup"))
            return;
        
        if (methodDecl.Body == null)
            return;
        
        // Look for AddAuthorization calls
        var authCalls = methodDecl.Body.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(i => ExtractMethodNameFromInvocation(i) == "AddAuthorization")
            .ToList();
        
        foreach (var authCall in authCalls)
        {
            // Look for AddPolicy calls within the lambda
            var policyDefs = authCall.DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .Where(i => ExtractMethodNameFromInvocation(i) == "AddPolicy")
                .ToList();
            
            foreach (var policyDef in policyDefs)
            {
                var policyName = ExtractPolicyName(policyDef);
                if (!string.IsNullOrEmpty(policyName))
                {
                    var lineNumber = policyDef.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                    
                    var policyChunk = new CodeMemory
                    {
                        Type = CodeMemoryType.Pattern,
                        Name = $"Policy: {policyName}",
                        Content = policyDef.ToString(),
                        FilePath = filePath,
                        Context = context,
                        LineNumber = lineNumber,
                        Metadata = new Dictionary<string, object>
                        {
                            ["chunk_type"] = "auth_policy",
                            ["policy_name"] = policyName,
                            ["framework"] = "aspnet-core",
                            ["layer"] = "Infra"
                        }
                    };
                    result.CodeElements.Add(policyChunk);
                    
                    result.Relationships.Add(new CodeRelationship
                    {
                        FromName = "Program",
                        ToName = policyName,
                        Type = RelationshipType.DefinesPolicy,
                        Context = context
                    });
                }
            }
        }
    }

    private string ExtractPolicyName(InvocationExpressionSyntax invocation)
    {
        var args = invocation.ArgumentList?.Arguments;
        if (args != null && args.Value.Count > 0)
        {
            return args.Value[0].ToString().Trim('"');
        }
        return string.Empty;
    }

    /// <summary>
    /// Extract configuration binding (IOptions)
    /// </summary>
    private void ExtractConfigurationBinding(
        MethodDeclarationSyntax methodDecl,
        string filePath,
        string context,
        ParseResult result)
    {
        var fileName = Path.GetFileName(filePath);
        if (!fileName.Contains("Program") && !fileName.Contains("Startup"))
            return;
        
        if (methodDecl.Body == null)
            return;
        
        var configureCalls = methodDecl.Body.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(i => ExtractMethodNameFromInvocation(i) == "Configure")
            .ToList();
        
        foreach (var configureCall in configureCalls)
        {
            var configType = ExtractConfigureType(configureCall);
            if (!string.IsNullOrEmpty(configType))
            {
                var lineNumber = configureCall.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                
                var configChunk = new CodeMemory
                {
                    Type = CodeMemoryType.Pattern,
                    Name = $"Config: {configType}",
                    Content = configureCall.ToString(),
                    FilePath = filePath,
                    Context = context,
                    LineNumber = lineNumber,
                    Metadata = new Dictionary<string, object>
                    {
                        ["chunk_type"] = "configuration_binding",
                        ["config_class"] = configType,
                        ["binding_method"] = "IOptions",
                        ["framework"] = "aspnet-core",
                        ["layer"] = "Infra"
                    }
                };
                result.CodeElements.Add(configChunk);
                
                result.Relationships.Add(new CodeRelationship
                {
                    FromName = "Program",
                    ToName = configType,
                    Type = RelationshipType.BindsConfig,
                    Context = context
                });
            }
        }
    }

    private string ExtractConfigureType(InvocationExpressionSyntax invocation)
    {
        if (invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
            memberAccess.Name is GenericNameSyntax genericName)
        {
            var typeArgs = genericName.TypeArgumentList.Arguments;
            if (typeArgs.Count == 1)
            {
                return typeArgs[0].ToString();
            }
        }
        return string.Empty;
    }

    /// <summary>
    /// Extract repository pattern implementations
    /// </summary>
    private void ExtractRepositoryPatterns(
        ClassDeclarationSyntax classDecl,
        string fullClassName,
        string context,
        ParseResult result)
    {
        if (!fullClassName.Contains("Repository"))
            return;
        
        var implements = classDecl.BaseList?.Types
            .Select(t => t.Type.ToString())
            .Where(t => t.StartsWith("I") && t.Contains("Repository"))
            .ToList() ?? new List<string>();
        
        foreach (var interfaceType in implements)
        {
            result.Relationships.Add(new CodeRelationship
            {
                FromName = fullClassName,
                ToName = interfaceType,
                Type = RelationshipType.Implements,
                Context = context,
                Properties = new Dictionary<string, object>
                {
                    ["pattern"] = "Repository"
                }
            });
        }
    }

    /// <summary>
    /// Extract Health Check implementations
    /// </summary>
    private void ExtractHealthChecks(
        ClassDeclarationSyntax classDecl,
        string fullClassName,
        string filePath,
        string context,
        ParseResult result)
    {
        var implementsHealthCheck = classDecl.BaseList?.Types
            .Any(t => t.Type.ToString() == "IHealthCheck") ?? false;
        
        if (!implementsHealthCheck)
            return;
        
        var lineNumber = classDecl.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
        
        var healthCheckChunk = new CodeMemory
        {
            Type = CodeMemoryType.Pattern,
            Name = $"HealthCheck: {fullClassName}",
            Content = classDecl.ToString(),
            FilePath = filePath,
            Context = context,
            LineNumber = lineNumber,
            Metadata = new Dictionary<string, object>
            {
                ["chunk_type"] = "health_check",
                ["check_name"] = fullClassName,
                ["framework"] = "aspnet-core",
                ["layer"] = "Infra"
            }
        };
        result.CodeElements.Add(healthCheckChunk);
        
        result.Relationships.Add(new CodeRelationship
        {
            FromName = "HealthChecks",
            ToName = fullClassName,
            Type = RelationshipType.Monitors,
            Context = context
        });
    }

    /// <summary>
    /// Enhance method with filter/caching/attribute metadata
    /// </summary>
    private void EnhanceMethodWithAttributes(
        MethodDeclarationSyntax methodDecl,
        CodeMemory methodMemory,
        ParseResult result,
        string context)
    {
        var attributes = methodDecl.AttributeLists.SelectMany(al => al.Attributes).ToList();
        
        // Response Caching
        var responseCacheAttr = attributes.FirstOrDefault(a => a.Name.ToString().Contains("ResponseCache"));
        if (responseCacheAttr != null)
        {
            methodMemory.Metadata["has_response_cache"] = true;
            methodMemory.Metadata["cache_framework"] = "aspnet-core";
        }
        
        // Service Filter / Type Filter
        var filterAttrs = attributes.Where(a => 
            a.Name.ToString().Contains("ServiceFilter") ||
            a.Name.ToString().Contains("TypeFilter") ||
            a.Name.ToString().Contains("Filter")).ToList();
        
        if (filterAttrs.Any())
        {
            methodMemory.Metadata["has_filters"] = true;
            methodMemory.Metadata["filter_count"] = filterAttrs.Count;
        }
        
        // Rate Limiting
        var rateLimitAttr = attributes.FirstOrDefault(a => 
            a.Name.ToString().Contains("RateLimit") ||
            a.Name.ToString().Contains("EnableRateLimiting"));
        
        if (rateLimitAttr != null)
        {
            methodMemory.Metadata["has_rate_limiting"] = true;
        }
    }

    /// <summary>
    /// Extract API versioning attributes
    /// </summary>
    private void ExtractApiVersioning(
        ClassDeclarationSyntax classDecl,
        string fullClassName,
        string filePath,
        string context,
        ParseResult result)
    {
        var classAttributes = classDecl.AttributeLists.SelectMany(al => al.Attributes).ToList();
        
        // [ApiVersion("1.0")]
        var versionAttrs = classAttributes.Where(a => a.Name.ToString().Contains("ApiVersion")).ToList();
        
        foreach (var versionAttr in versionAttrs)
        {
            var versionArg = versionAttr.ArgumentList?.Arguments.FirstOrDefault()?.ToString().Trim('"', ' ');
            if (string.IsNullOrEmpty(versionArg))
                continue;
            
            var lineNumber = versionAttr.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
            
            var versionChunk = new CodeMemory
            {
                Type = CodeMemoryType.Pattern,
                Name = $"ApiVersion: {versionArg}",
                Content = versionAttr.ToString(),
                FilePath = filePath,
                Context = context,
                LineNumber = lineNumber,
                Metadata = new Dictionary<string, object>
                {
                    ["chunk_type"] = "api_version",
                    ["version"] = versionArg,
                    ["controller"] = fullClassName,
                    ["framework"] = "aspnet-core"
                }
            };
            result.CodeElements.Add(versionChunk);
            
            result.Relationships.Add(new CodeRelationship
            {
                FromName = fullClassName,
                ToName = $"ApiVersion({versionArg})",
                Type = RelationshipType.HasApiVersion,
                Context = context,
                Properties = new Dictionary<string, object>
                {
                    ["version"] = versionArg
                }
            });
        }
        
        // Check methods for MapToApiVersion
        var methods = classDecl.Members.OfType<MethodDeclarationSyntax>();
        foreach (var method in methods)
        {
            var methodAttributes = method.AttributeLists.SelectMany(al => al.Attributes).ToList();
            var mapToVersionAttr = methodAttributes.FirstOrDefault(a => a.Name.ToString().Contains("MapToApiVersion"));
            
            if (mapToVersionAttr != null)
            {
                var versionArg = mapToVersionAttr.ArgumentList?.Arguments.FirstOrDefault()?.ToString().Trim('"', ' ');
                var methodName = method.Identifier.Text;
                
                result.Relationships.Add(new CodeRelationship
                {
                    FromName = $"{fullClassName}.{methodName}",
                    ToName = $"ApiVersion({versionArg})",
                    Type = RelationshipType.HasApiVersion,
                    Context = context,
                    Properties = new Dictionary<string, object>
                    {
                        ["version"] = versionArg ?? "unknown",
                        ["mapped"] = true
                    }
                });
            }
        }
    }

    /// <summary>
    /// Extract exception filters
    /// </summary>
    private void ExtractExceptionFilters(
        ClassDeclarationSyntax classDecl,
        string fullClassName,
        string filePath,
        string context,
        ParseResult result)
    {
        var implementsExceptionFilter = classDecl.BaseList?.Types
            .Any(t => t.Type.ToString().Contains("ExceptionFilter") || 
                     t.Type.ToString() == "IExceptionFilter" ||
                     t.Type.ToString() == "IAsyncExceptionFilter") ?? false;
        
        if (!implementsExceptionFilter)
            return;
        
        var lineNumber = classDecl.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
        
        var filterChunk = new CodeMemory
        {
            Type = CodeMemoryType.Pattern,
            Name = $"ExceptionFilter: {fullClassName}",
            Content = classDecl.ToString(),
            FilePath = filePath,
            Context = context,
            LineNumber = lineNumber,
            Metadata = new Dictionary<string, object>
            {
                ["chunk_type"] = "exception_filter",
                ["filter_name"] = fullClassName,
                ["framework"] = "aspnet-core",
                ["layer"] = "API",
                ["is_async"] = classDecl.BaseList?.Types.Any(t => t.Type.ToString().Contains("Async")) ?? false
            }
        };
        result.CodeElements.Add(filterChunk);
        
        // Find OnException or OnExceptionAsync method
        var exceptionMethods = classDecl.Members.OfType<MethodDeclarationSyntax>()
            .Where(m => m.Identifier.Text == "OnException" || m.Identifier.Text == "OnExceptionAsync");
        
        foreach (var method in exceptionMethods)
        {
            // Try to detect which exception types are handled
            var methodBody = method.Body?.ToString() ?? string.Empty;
            var catchMatches = Regex.Matches(methodBody, @"catch\s*\(\s*(\w+)");
            var isMatches = Regex.Matches(methodBody, @"is\s+(\w+Exception)");
            
            var handledExceptions = catchMatches.Cast<Match>()
                .Concat(isMatches.Cast<Match>())
                .Select(m => m.Groups[1].Value)
                .Distinct()
                .ToList();
            
            foreach (var exceptionType in handledExceptions)
            {
                result.Relationships.Add(new CodeRelationship
                {
                    FromName = fullClassName,
                    ToName = exceptionType,
                    Type = RelationshipType.HandlesException,
                    Context = context,
                    Properties = new Dictionary<string, object>
                    {
                        ["filter_type"] = "ExceptionFilter"
                    }
                });
            }
        }
    }

    /// <summary>
    /// Extract Swagger/OpenAPI configuration
    /// </summary>
    private void ExtractSwaggerConfig(
        SyntaxNode root,
        string filePath,
        string context,
        ParseResult result)
    {
        if (!filePath.Contains("Program.cs") && !filePath.Contains("Startup.cs"))
            return;
        
        var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();
        
        // AddSwaggerGen
        var swaggerGenCalls = invocations.Where(inv => 
            inv.ToString().Contains("AddSwaggerGen"));
        
        foreach (var call in swaggerGenCalls)
        {
            var lineNumber = call.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
            
            var swaggerChunk = new CodeMemory
            {
                Type = CodeMemoryType.Pattern,
                Name = "SwaggerConfig",
                Content = call.ToString(),
                FilePath = filePath,
                Context = context,
                LineNumber = lineNumber,
                Metadata = new Dictionary<string, object>
                {
                    ["chunk_type"] = "swagger_config",
                    ["framework"] = "aspnet-core",
                    ["api_docs"] = true
                }
            };
            result.CodeElements.Add(swaggerChunk);
            
            result.Relationships.Add(new CodeRelationship
            {
                FromName = "SwaggerConfig",
                ToName = "API",
                Type = RelationshipType.Documents,
                Context = context,
                Properties = new Dictionary<string, object>
                {
                    ["tool"] = "Swagger/OpenAPI"
                }
            });
        }
        
        // OperationFilter, SchemaFilter
        var filterCalls = invocations.Where(inv => 
            inv.ToString().Contains("OperationFilter") || 
            inv.ToString().Contains("SchemaFilter"));
        
        foreach (var call in filterCalls)
        {
            var genericMatch = Regex.Match(call.ToString(), @"<(\w+)>");
            if (genericMatch.Success)
            {
                var filterType = genericMatch.Groups[1].Value;
                
                result.Relationships.Add(new CodeRelationship
                {
                    FromName = "SwaggerConfig",
                    ToName = filterType,
                    Type = RelationshipType.Filters,
                    Context = context,
                    Properties = new Dictionary<string, object>
                    {
                        ["filter_category"] = call.ToString().Contains("Operation") ? "OperationFilter" : "SchemaFilter"
                    }
                });
            }
        }
    }

    /// <summary>
    /// Extract CORS policies
    /// </summary>
    private void ExtractCorsPolicies(
        SyntaxNode root,
        string filePath,
        string context,
        ParseResult result)
    {
        if (!filePath.Contains("Program.cs") && !filePath.Contains("Startup.cs"))
            return;
        
        var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();
        
        // AddCors
        var addCorsCalls = invocations.Where(inv => inv.ToString().Contains("AddCors"));
        
        foreach (var call in addCorsCalls)
        {
            var lineNumber = call.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
            var callText = call.ToString();
            
            // Extract policy name
            var policyMatch = Regex.Match(callText, @"AddPolicy\s*\(\s*""([^""]+)""");
            var policyName = policyMatch.Success ? policyMatch.Groups[1].Value : "DefaultCorsPolicy";
            
            // Extract allowed origins
            var originsMatches = Regex.Matches(callText, @"WithOrigins\s*\([^)]*""([^""]+)""");
            var allowAnyOrigin = callText.Contains("AllowAnyOrigin");
            
            var corsChunk = new CodeMemory
            {
                Type = CodeMemoryType.Pattern,
                Name = $"CorsPolicy: {policyName}",
                Content = call.ToString(),
                FilePath = filePath,
                Context = context,
                LineNumber = lineNumber,
                Metadata = new Dictionary<string, object>
                {
                    ["chunk_type"] = "cors_policy",
                    ["policy_name"] = policyName,
                    ["allow_any_origin"] = allowAnyOrigin,
                    ["framework"] = "aspnet-core"
                }
            };
            result.CodeElements.Add(corsChunk);
            
            if (allowAnyOrigin)
            {
                result.Relationships.Add(new CodeRelationship
                {
                    FromName = $"CorsPolicy({policyName})",
                    ToName = "*",
                    Type = RelationshipType.AllowsOrigin,
                    Context = context,
                    Properties = new Dictionary<string, object>
                    {
                        ["any_origin"] = true
                    }
                });
            }
            else
            {
                foreach (Match match in originsMatches)
                {
                    var origin = match.Groups[1].Value;
                    result.Relationships.Add(new CodeRelationship
                    {
                        FromName = $"CorsPolicy({policyName})",
                        ToName = origin,
                        Type = RelationshipType.AllowsOrigin,
                        Context = context
                    });
                }
            }
        }
    }

    /// <summary>
    /// Extract response caching configuration
    /// </summary>
    private void ExtractResponseCaching(
        MethodDeclarationSyntax methodDecl,
        string fullMethodName,
        string filePath,
        string context,
        ParseResult result)
    {
        var attributes = methodDecl.AttributeLists.SelectMany(al => al.Attributes).ToList();
        var responseCacheAttr = attributes.FirstOrDefault(a => a.Name.ToString().Contains("ResponseCache"));
        
        if (responseCacheAttr == null)
            return;
        
        var lineNumber = responseCacheAttr.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
        var attrText = responseCacheAttr.ToString();
        
        // Extract duration
        var durationMatch = Regex.Match(attrText, @"Duration\s*=\s*(\d+)");
        var duration = durationMatch.Success ? durationMatch.Groups[1].Value : "unknown";
        
        // Extract cache profile
        var profileMatch = Regex.Match(attrText, @"CacheProfileName\s*=\s*""([^""]+)""");
        var profileName = profileMatch.Success ? profileMatch.Groups[1].Value : null;
        
        var cacheChunk = new CodeMemory
        {
            Type = CodeMemoryType.Pattern,
            Name = $"ResponseCache: {fullMethodName}",
            Content = attrText,
            FilePath = filePath,
            Context = context,
            LineNumber = lineNumber,
            Metadata = new Dictionary<string, object>
            {
                ["chunk_type"] = "response_cache",
                ["method"] = fullMethodName,
                ["duration"] = duration,
                ["framework"] = "aspnet-core"
            }
        };
        
        if (profileName != null)
            cacheChunk.Metadata["cache_profile"] = profileName;
        
        result.CodeElements.Add(cacheChunk);
        
        result.Relationships.Add(new CodeRelationship
        {
            FromName = fullMethodName,
            ToName = profileName ?? "ResponseCache",
            Type = RelationshipType.Caches,
            Context = context,
            Properties = new Dictionary<string, object>
            {
                ["duration_seconds"] = duration
            }
        });
    }

    /// <summary>
    /// Extract custom model binders
    /// </summary>
    private void ExtractModelBinders(
        ClassDeclarationSyntax classDecl,
        string fullClassName,
        string filePath,
        string context,
        ParseResult result)
    {
        var implementsBinder = classDecl.BaseList?.Types
            .Any(t => t.Type.ToString() == "IModelBinder") ?? false;
        
        if (!implementsBinder)
            return;
        
        var lineNumber = classDecl.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
        
        // Try to detect which type is being bound
        var bindMethod = classDecl.Members.OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(m => m.Identifier.Text == "BindModelAsync");
        
        string? boundType = null;
        if (bindMethod != null)
        {
            var methodBody = bindMethod.Body?.ToString() ?? string.Empty;
            var typeMatch = Regex.Match(methodBody, @"typeof\s*\(\s*(\w+)\s*\)");
            boundType = typeMatch.Success ? typeMatch.Groups[1].Value : null;
        }
        
        var binderChunk = new CodeMemory
        {
            Type = CodeMemoryType.Pattern,
            Name = $"ModelBinder: {fullClassName}",
            Content = classDecl.ToString(),
            FilePath = filePath,
            Context = context,
            LineNumber = lineNumber,
            Metadata = new Dictionary<string, object>
            {
                ["chunk_type"] = "model_binder",
                ["binder_name"] = fullClassName,
                ["framework"] = "aspnet-core",
                ["layer"] = "API"
            }
        };
        
        if (boundType != null)
            binderChunk.Metadata["bound_type"] = boundType;
        
        result.CodeElements.Add(binderChunk);
        
        if (boundType != null)
        {
            result.Relationships.Add(new CodeRelationship
            {
                FromName = fullClassName,
                ToName = boundType,
                Type = RelationshipType.Binds,
                Context = context,
                Properties = new Dictionary<string, object>
                {
                    ["binder_type"] = "Custom"
                }
            });
        }
    }

    /// <summary>
    /// Extract action filters
    /// </summary>
    private void ExtractActionFilters(
        ClassDeclarationSyntax classDecl,
        string fullClassName,
        string filePath,
        string context,
        ParseResult result)
    {
        var implementsFilter = classDecl.BaseList?.Types
            .Any(t => t.Type.ToString().Contains("ActionFilter") ||
                     t.Type.ToString() == "IActionFilter" ||
                     t.Type.ToString() == "IAsyncActionFilter" ||
                     t.Type.ToString() == "IResultFilter" ||
                     t.Type.ToString() == "IAsyncResultFilter") ?? false;
        
        if (!implementsFilter)
            return;
        
        var lineNumber = classDecl.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
        var filterType = classDecl.BaseList?.Types.FirstOrDefault()?.Type.ToString() ?? "ActionFilter";
        
        var filterChunk = new CodeMemory
        {
            Type = CodeMemoryType.Pattern,
            Name = $"ActionFilter: {fullClassName}",
            Content = classDecl.ToString(),
            FilePath = filePath,
            Context = context,
            LineNumber = lineNumber,
            Metadata = new Dictionary<string, object>
            {
                ["chunk_type"] = "action_filter",
                ["filter_name"] = fullClassName,
                ["filter_type"] = filterType,
                ["framework"] = "aspnet-core",
                ["layer"] = "API",
                ["is_async"] = filterType.Contains("Async")
            }
        };
        result.CodeElements.Add(filterChunk);
        
        // Check for [ServiceFilter] or [TypeFilter] usage on methods/controllers
        result.Relationships.Add(new CodeRelationship
        {
            FromName = "FilterPipeline",
            ToName = fullClassName,
            Type = RelationshipType.Filters,
            Context = context,
            Properties = new Dictionary<string, object>
            {
                ["filter_category"] = filterType.Contains("Result") ? "ResultFilter" : "ActionFilter"
            }
        });
    }

    /// <summary>
    /// Extract rate limiting configuration
    /// </summary>
    private void ExtractRateLimiting(
        SyntaxNode root,
        string filePath,
        string context,
        ParseResult result)
    {
        if (!filePath.Contains("Program.cs") && !filePath.Contains("Startup.cs"))
            return;
        
        var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();
        
        // AddRateLimiter
        var rateLimiterCalls = invocations.Where(inv => inv.ToString().Contains("AddRateLimiter"));
        
        foreach (var call in rateLimiterCalls)
        {
            var lineNumber = call.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
            var callText = call.ToString();
            
            // Extract policy name
            var policyMatch = Regex.Match(callText, @"AddPolicy[^(]*\(\s*""([^""]+)""");
            var policyName = policyMatch.Success ? policyMatch.Groups[1].Value : "DefaultRateLimitPolicy";
            
            // Extract limiter type
            var limiterType = "Unknown";
            if (callText.Contains("FixedWindowLimiter")) limiterType = "FixedWindow";
            else if (callText.Contains("SlidingWindowLimiter")) limiterType = "SlidingWindow";
            else if (callText.Contains("TokenBucketLimiter")) limiterType = "TokenBucket";
            else if (callText.Contains("ConcurrencyLimiter")) limiterType = "Concurrency";
            
            var rateLimitChunk = new CodeMemory
            {
                Type = CodeMemoryType.Pattern,
                Name = $"RateLimitPolicy: {policyName}",
                Content = call.ToString(),
                FilePath = filePath,
                Context = context,
                LineNumber = lineNumber,
                Metadata = new Dictionary<string, object>
                {
                    ["chunk_type"] = "rate_limit_policy",
                    ["policy_name"] = policyName,
                    ["limiter_type"] = limiterType,
                    ["framework"] = "aspnet-core"
                }
            };
            result.CodeElements.Add(rateLimitChunk);
            
            result.Relationships.Add(new CodeRelationship
            {
                FromName = "API",
                ToName = $"RateLimitPolicy({policyName})",
                Type = RelationshipType.RateLimits,
                Context = context,
                Properties = new Dictionary<string, object>
                {
                    ["limiter_type"] = limiterType
                }
            });
        }
        
        // Check for [EnableRateLimiting] attributes
        var attributes = root.DescendantNodes()
            .OfType<AttributeSyntax>()
            .Where(a => a.Name.ToString().Contains("EnableRateLimiting"));
        
        foreach (var attr in attributes)
        {
            var policyArg = attr.ArgumentList?.Arguments.FirstOrDefault()?.ToString().Trim('"', ' ');
            if (string.IsNullOrEmpty(policyArg))
                continue;
            
            // Find the parent method or class
            var parent = attr.Parent?.Parent?.Parent;
            string targetName = "Unknown";
            
            if (parent is MethodDeclarationSyntax method)
            {
                targetName = method.Identifier.Text;
            }
            else if (parent is ClassDeclarationSyntax cls)
            {
                targetName = cls.Identifier.Text;
            }
            
            result.Relationships.Add(new CodeRelationship
            {
                FromName = targetName,
                ToName = $"RateLimitPolicy({policyArg})",
                Type = RelationshipType.RateLimits,
                Context = context,
                Properties = new Dictionary<string, object>
                {
                    ["applied_via"] = "EnableRateLimiting"
                }
            });
        }
    }

    #endregion
    
    #region Smart Embedding Helpers
    
    /// <summary>
    /// Extracts XML documentation summary for a syntax node
    /// </summary>
    private static string ExtractXmlSummary(SyntaxNode node)
    {
        var trivia = node.GetLeadingTrivia();
        var docComments = trivia.Where(t => t.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) ||
                                            t.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia))
                                .ToList();
        
        if (!docComments.Any())
            return string.Empty;
        
        var fullDoc = string.Join("\n", docComments.Select(c => c.ToString()));
        
        // Extract <summary> content
        var summaryMatch = System.Text.RegularExpressions.Regex.Match(fullDoc, @"<summary>(.*?)</summary>", 
            System.Text.RegularExpressions.RegexOptions.Singleline);
        
        if (!summaryMatch.Success)
            return string.Empty;
        
        var summary = summaryMatch.Groups[1].Value
            .Replace("///", "")
            .Replace("*", "")
            .Trim();
        
        // Clean up whitespace
        return System.Text.RegularExpressions.Regex.Replace(summary, @"\s+", " ").Trim();
    }
    
    /// <summary>
    /// Builds a clean signature for a class
    /// </summary>
    private static string BuildClassSignature(ClassDeclarationSyntax classDecl)
    {
        var modifiers = string.Join(" ", classDecl.Modifiers.Select(m => m.Text));
        var baselist = classDecl.BaseList != null ? " : " + classDecl.BaseList.ToString() : "";
        var typeParams = classDecl.TypeParameterList?.ToString() ?? "";
        
        return $"{modifiers} class {classDecl.Identifier.Text}{typeParams}{baselist}".Trim();
    }
    
    /// <summary>
    /// Builds a clean signature for a method
    /// </summary>
    private static string BuildMethodSignature(MethodDeclarationSyntax methodDecl)
    {
        var modifiers = string.Join(" ", methodDecl.Modifiers.Select(m => m.Text));
        var returnType = methodDecl.ReturnType.ToString();
        var methodName = methodDecl.Identifier.Text;
        var parameters = methodDecl.ParameterList.ToString();
        var typeParams = methodDecl.TypeParameterList?.ToString() ?? "";
        
        return $"{modifiers} {returnType} {methodName}{typeParams}{parameters}".Trim();
    }
    
    /// <summary>
    /// Extracts semantic tags from a class
    /// </summary>
    private static List<string> ExtractClassTags(ClassDeclarationSyntax classDecl, string namespaceName)
    {
        var tags = new List<string>();
        
        // Modifiers
        if (classDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword))) tags.Add("public");
        if (classDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.InternalKeyword))) tags.Add("internal");
        if (classDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.AbstractKeyword))) tags.Add("abstract");
        if (classDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.SealedKeyword))) tags.Add("sealed");
        if (classDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword))) tags.Add("static");
        
        // Type parameters
        if (classDecl.TypeParameterList != null) tags.Add("generic");
        
        // Framework/pattern detection
        var className = classDecl.Identifier.Text;
        if (className.EndsWith("Controller")) tags.Add("controller");
        if (className.EndsWith("Service")) tags.Add("service");
        if (className.EndsWith("Repository")) tags.Add("repository");
        if (className.EndsWith("Validator")) tags.Add("validator");
        if (className.EndsWith("Handler")) tags.Add("handler");
        if (className.Contains("DbContext")) tags.Add("dbcontext");
        if (className.EndsWith("Middleware")) tags.Add("middleware");
        if (className.EndsWith("Filter")) tags.Add("filter");
        
        // Check for async methods
        var hasAsyncMethods = classDecl.Members.OfType<MethodDeclarationSyntax>()
            .Any(m => m.Modifiers.Any(mod => mod.IsKind(SyntaxKind.AsyncKeyword)));
        if (hasAsyncMethods) tags.Add("async");
        
        // Namespace-based tags
        if (namespaceName.Contains(".Controllers")) tags.Add("api");
        if (namespaceName.Contains(".Services")) tags.Add("business-logic");
        if (namespaceName.Contains(".Data") || namespaceName.Contains(".Infrastructure")) tags.Add("data-access");
        if (namespaceName.Contains(".Models") || namespaceName.Contains(".Entities")) tags.Add("model");
        
        return tags;
    }
    
    /// <summary>
    /// Extracts semantic tags from a method
    /// </summary>
    private static List<string> ExtractMethodTags(MethodDeclarationSyntax methodDecl)
    {
        var tags = new List<string>();
        
        // Modifiers
        if (methodDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword))) tags.Add("public");
        if (methodDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.PrivateKeyword))) tags.Add("private");
        if (methodDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.ProtectedKeyword))) tags.Add("protected");
        if (methodDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword))) tags.Add("static");
        if (methodDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.AsyncKeyword))) tags.Add("async");
        if (methodDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.VirtualKeyword))) tags.Add("virtual");
        if (methodDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.OverrideKeyword))) tags.Add("override");
        
        // Return type analysis
        var returnType = methodDecl.ReturnType.ToString();
        if (returnType.Contains("Task")) tags.Add("async-task");
        if (returnType.Contains("IActionResult") || returnType.Contains("ActionResult")) tags.Add("api-endpoint");
        if (returnType == "void" || returnType == "Task") tags.Add("void-return");
        
        // Method name patterns
        var methodName = methodDecl.Identifier.Text;
        if (methodName.StartsWith("Get")) tags.Add("query");
        if (methodName.StartsWith("Create") || methodName.StartsWith("Add")) tags.Add("create");
        if (methodName.StartsWith("Update") || methodName.StartsWith("Modify")) tags.Add("update");
        if (methodName.StartsWith("Delete") || methodName.StartsWith("Remove")) tags.Add("delete");
        if (methodName.StartsWith("Validate")) tags.Add("validation");
        if (methodName.Contains("Async")) tags.Add("async-method");
        
        // Attribute detection
        var attributes = methodDecl.AttributeLists.SelectMany(al => al.Attributes).Select(a => a.Name.ToString()).ToList();
        if (attributes.Any(a => a.Contains("HttpGet"))) tags.Add("http-get");
        if (attributes.Any(a => a.Contains("HttpPost"))) tags.Add("http-post");
        if (attributes.Any(a => a.Contains("HttpPut"))) tags.Add("http-put");
        if (attributes.Any(a => a.Contains("HttpDelete"))) tags.Add("http-delete");
        if (attributes.Any(a => a.Contains("Authorize"))) tags.Add("secured");
        if (attributes.Any(a => a.Contains("AllowAnonymous"))) tags.Add("anonymous");
        
        return tags;
    }
    
    /// <summary>
    /// Extracts dependencies from a class (constructor parameters, injected services)
    /// </summary>
    private static List<string> ExtractClassDependencies(ClassDeclarationSyntax classDecl)
    {
        var dependencies = new HashSet<string>();
        
        // Find constructor parameters
        var constructors = classDecl.Members.OfType<ConstructorDeclarationSyntax>();
        foreach (var ctor in constructors)
        {
            foreach (var param in ctor.ParameterList.Parameters)
            {
                var typeName = param.Type?.ToString();
                if (!string.IsNullOrEmpty(typeName))
                {
                    dependencies.Add(typeName);
                }
            }
        }
        
        // Find injected fields/properties
        var fields = classDecl.Members.OfType<FieldDeclarationSyntax>();
        foreach (var field in fields)
        {
            if (field.Modifiers.Any(m => m.IsKind(SyntaxKind.PrivateKeyword) || 
                                         m.IsKind(SyntaxKind.ReadOnlyKeyword)))
            {
                var typeName = field.Declaration.Type.ToString();
                if (typeName.StartsWith("I") && char.IsUpper(typeName.Length > 1 ? typeName[1] : ' '))
                {
                    dependencies.Add(typeName);
                }
            }
        }
        
        return dependencies.ToList();
    }
    
    /// <summary>
    /// Extracts dependencies from a method (parameter types, return type)
    /// </summary>
    private static List<string> ExtractMethodDependencies(MethodDeclarationSyntax methodDecl)
    {
        var dependencies = new HashSet<string>();
        
        // Return type
        var returnType = methodDecl.ReturnType.ToString();
        if (!string.IsNullOrEmpty(returnType) && returnType != "void")
        {
            dependencies.Add(returnType);
        }
        
        // Parameters
        foreach (var param in methodDecl.ParameterList.Parameters)
        {
            var typeName = param.Type?.ToString();
            if (!string.IsNullOrEmpty(typeName))
            {
                dependencies.Add(typeName);
            }
        }
        
        return dependencies.ToList();
    }
    
    #endregion
}

