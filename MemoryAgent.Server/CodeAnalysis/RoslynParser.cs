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

    public async Task<ParseResult> ParseFileAsync(string filePath, string? context = null, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return new ParseResult { Errors = { $"File not found: {filePath}" } };
            }

            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            
            // Route to appropriate parser based on file extension
            return extension switch
            {
                ".cshtml" or ".razor" => await Task.Run(() => RazorParser.ParseRazorFile(filePath, context), cancellationToken),
                ".py" => await Task.Run(() => PythonParser.ParsePythonFile(filePath, context), cancellationToken),
                ".md" or ".markdown" => await new MarkdownParser(_loggerFactory.CreateLogger<MarkdownParser>()).ParseFileAsync(filePath, context, cancellationToken),
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

    public Task<ParseResult> ParseCodeAsync(string code, string filePath, string? context = null, CancellationToken cancellationToken = default)
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

            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing code for file: {FilePath}", filePath);
            result.Errors.Add($"Error parsing code: {ex.Message}");
            return Task.FromResult(result);
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

        // Create class memory
        var classMemory = new CodeMemory
        {
            Type = CodeMemoryType.Class,
            Name = fullName,
            Content = classDecl.ToString(),
            FilePath = filePath,
            Context = context,
            LineNumber = lineNumber,
            Metadata = new Dictionary<string, object>
            {
                ["namespace"] = namespaceName,
                ["is_abstract"] = classDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.AbstractKeyword)),
                ["is_static"] = classDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword)),
                ["is_sealed"] = classDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.SealedKeyword)),
                ["access_modifier"] = GetAccessModifier(classDecl.Modifiers),
                ["language"] = "csharp",
                ["layer"] = DetermineLayer(className, namespaceName),
                ["bounded_context"] = DetermineBoundedContext(namespaceName)
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

        // Extract methods
        var methods = classDecl.Members.OfType<MethodDeclarationSyntax>();
        foreach (var method in methods)
        {
            ExtractMethod(method, fullName, className, filePath, context, result);
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
        ParseResult result)
    {
        var methodName = methodDecl.Identifier.Text;
        var lineNumber = methodDecl.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

        var methodMemory = new CodeMemory
        {
            Type = CodeMemoryType.Method,
            Name = $"{fullClassName}.{methodName}",
            Content = methodDecl.ToString(),
            FilePath = filePath,
            Context = context,
            LineNumber = lineNumber,
            Metadata = new Dictionary<string, object>
            {
                ["class_name"] = fullClassName,
                ["return_type"] = methodDecl.ReturnType.ToString(),
                ["is_async"] = methodDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.AsyncKeyword)),
                ["is_static"] = methodDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword)),
                ["is_virtual"] = methodDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.VirtualKeyword)),
                ["is_override"] = methodDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.OverrideKeyword)),
                ["access_modifier"] = GetAccessModifier(methodDecl.Modifiers),
                ["parameter_count"] = methodDecl.ParameterList.Parameters.Count
            }
        };
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
        
        // Extract method calls
        ExtractMethodCalls(methodDecl, $"{fullClassName}.{methodName}", context, result);
        
        // Extract attributes on method
        ExtractAttributes(methodDecl.AttributeLists, $"{fullClassName}.{methodName}", context, result);
        
        // Extract exception handling
        ExtractExceptionHandling(methodDecl, $"{fullClassName}.{methodName}", context, result);
        
        // SEMANTIC CHUNKING: Enhance with ASP.NET action method semantics
        EnhanceWithActionMethodSemantics(methodDecl, fullClassName, methodName, methodMemory, result, context);
        
        // SEMANTIC CHUNKING: Enhance with EF query semantics
        EnhanceWithEFQuerySemantics(methodDecl, $"{fullClassName}.{methodName}", methodMemory, result, context);
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
    /// Extract method calls from method body
    /// </summary>
    private void ExtractMethodCalls(MethodDeclarationSyntax methodDecl, string fullMethodName, string context, ParseResult result)
    {
        if (methodDecl.Body == null && methodDecl.ExpressionBody == null)
            return;

        var invocations = methodDecl.DescendantNodes().OfType<InvocationExpressionSyntax>();
        
        foreach (var invocation in invocations)
        {
            var calledMethodName = ExtractMethodNameFromInvocation(invocation);
            if (!string.IsNullOrWhiteSpace(calledMethodName))
            {
                result.Relationships.Add(new CodeRelationship
                {
                    FromName = fullMethodName,
                    ToName = calledMethodName,
                    Type = RelationshipType.Calls,
                    Context = context
                });
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
    /// Extract method name from invocation expression
    /// </summary>
    private string ExtractMethodNameFromInvocation(InvocationExpressionSyntax invocation)
    {
        return invocation.Expression switch
        {
            IdentifierNameSyntax identifierName => identifierName.Identifier.Text,
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.Text,
            GenericNameSyntax genericName => genericName.Identifier.Text,
            _ => invocation.Expression.ToString()
        };
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
                Type = CodeMemoryType.Other,
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

    #endregion
}

