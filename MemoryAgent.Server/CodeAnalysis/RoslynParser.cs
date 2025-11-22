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
                ["access_modifier"] = GetAccessModifier(classDecl.Modifiers)
            }
        };
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
}

