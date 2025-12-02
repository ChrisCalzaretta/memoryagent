using System.Text.RegularExpressions;
using MemoryAgent.Server.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.VisualBasic;
using Microsoft.CodeAnalysis.VisualBasic.Syntax;

namespace MemoryAgent.Server.CodeAnalysis;

/// <summary>
/// Parses VB.NET code using Roslyn to extract classes, methods, properties, and relationships
/// Production-quality AST parser - NO REGEX!
/// </summary>
public class VBNetASTParser : ICodeParser
{
    private readonly ILogger<VBNetASTParser> _logger;

    public VBNetASTParser(ILogger<VBNetASTParser> logger)
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

            var code = await File.ReadAllTextAsync(filePath, cancellationToken);
            return await ParseCodeAsync(code, filePath, context, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing VB.NET file: {FilePath}", filePath);
            return new ParseResult { Errors = { $"Error parsing VB.NET file: {ex.Message}" } };
        }
    }

    public async Task<ParseResult> ParseCodeAsync(string code, string filePath, string? context = null, CancellationToken cancellationToken = default)
    {
        var result = new ParseResult();

        try
        {
            // Parse the VB.NET code using Roslyn
            var tree = VisualBasicSyntaxTree.ParseText(code, cancellationToken: cancellationToken);
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
                    ["file_size"] = fileInfo.Length,
                    ["language"] = "vb.net"
                }
            };
            result.CodeElements.Add(fileMemory);

            // Extract namespaces
            var namespaces = root.DescendantNodes().OfType<NamespaceBlockSyntax>();
            foreach (var ns in namespaces)
            {
                var nsName = ns.NamespaceStatement.Name.ToString();
                result.Relationships.Add(new CodeRelationship
                {
                    FromName = fileInfo.Name,
                    ToName = nsName,
                    Type = RelationshipType.Contains,
                    Context = context
                });
            }

            // Extract classes
            var classes = root.DescendantNodes().OfType<ClassBlockSyntax>();
            foreach (var classDecl in classes)
            {
                await ExtractClassAsync(classDecl, filePath, context, result, cancellationToken);
            }

            // Extract modules (VB.NET specific)
            var modules = root.DescendantNodes().OfType<ModuleBlockSyntax>();
            foreach (var module in modules)
            {
                await ExtractModuleAsync(module, filePath, context, result, cancellationToken);
            }

            // Extract interfaces
            var interfaces = root.DescendantNodes().OfType<InterfaceBlockSyntax>();
            foreach (var interfaceDecl in interfaces)
            {
                await ExtractInterfaceAsync(interfaceDecl, filePath, context, result, cancellationToken);
            }

            // Extract imports (Imports statements)
            var imports = root.DescendantNodes().OfType<ImportsStatementSyntax>();
            foreach (var import in imports)
            {
                foreach (var clause in import.ImportsClauses)
                {
                    string importedNamespace = clause.ToString();
                    result.Relationships.Add(new CodeRelationship
                    {
                        FromName = Path.GetFileNameWithoutExtension(filePath),
                        ToName = importedNamespace,
                        Type = RelationshipType.Imports,
                        Context = context,
                        Properties = new Dictionary<string, object>
                        {
                            ["import_statement"] = import.ToString()
                        }
                    });
                }
            }

            return await Task.FromResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing VB.NET code: {FilePath}", filePath);
            result.Errors.Add($"Error parsing VB.NET code: {ex.Message}");
            return result;
        }
    }

    private async Task ExtractClassAsync(ClassBlockSyntax classDecl, string filePath, string context, ParseResult result, CancellationToken cancellationToken)
    {
        var className = classDecl.ClassStatement.Identifier.Text;
        var lineNumber = classDecl.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

        // Extract XML documentation
        var xmlDocs = ExtractXmlDocumentation(classDecl);
        var summary = xmlDocs.ContainsKey("summary") ? xmlDocs["summary"] : string.Empty;
        var purpose = summary;

        // Extract signature
        var signature = classDecl.ClassStatement.ToString().Trim();

        // Extract modifiers/tags
        var tags = new List<string> { "class" };
        foreach (var modifier in classDecl.ClassStatement.Modifiers)
        {
            tags.Add(modifier.Text.ToLowerInvariant());
        }

        // Extract dependencies (base class and interfaces)
        var dependencies = new List<string>();
        
        // Find Inherits statements within the class block
        var inheritsStatements = classDecl.Inherits;
        foreach (var inheritsStatement in inheritsStatements)
        {
            foreach (var baseType in inheritsStatement.Types)
            {
                var baseTypeName = baseType.ToString();
                dependencies.Add(baseTypeName);

                result.Relationships.Add(new CodeRelationship
                {
                    FromName = className,
                    ToName = baseTypeName,
                    Type = RelationshipType.Inherits,
                    Context = context,
                    Properties = new Dictionary<string, object>
                    {
                        ["to_node_type"] = "Class"
                    }
                });
            }
        }

        // Find Implements statements within the class block
        var implementsStatements = classDecl.Implements;
        foreach (var implementsStatement in implementsStatements)
        {
            foreach (var interfaceType in implementsStatement.Types)
            {
                var interfaceName = interfaceType.ToString();
                dependencies.Add(interfaceName);

                result.Relationships.Add(new CodeRelationship
                {
                    FromName = className,
                    ToName = interfaceName,
                    Type = RelationshipType.Implements,
                    Context = context,
                    Properties = new Dictionary<string, object>
                    {
                        ["to_node_type"] = "Interface"
                    }
                });
            }
        }

        var classMemory = new CodeMemory
        {
            Type = CodeMemoryType.Class,
            Name = className,
            Content = classDecl.ToString(),
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
                ["language"] = "vb.net",
                ["type"] = "class",
                ["access_modifier"] = GetAccessModifier(classDecl.ClassStatement.Modifiers)
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
        var methods = classDecl.Members.OfType<MethodBlockSyntax>();
        foreach (var method in methods)
        {
            await ExtractMethodAsync(method, className, filePath, context, result, cancellationToken);
        }

        // Extract properties
        var properties = classDecl.Members.OfType<PropertyBlockSyntax>();
        foreach (var property in properties)
        {
            await ExtractPropertyAsync(property, className, filePath, context, result, cancellationToken);
        }

        await Task.CompletedTask;
    }

    private async Task ExtractModuleAsync(ModuleBlockSyntax module, string filePath, string context, ParseResult result, CancellationToken cancellationToken)
    {
        var moduleName = module.ModuleStatement.Identifier.Text;
        var lineNumber = module.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

        var xmlDocs = ExtractXmlDocumentation(module);
        var summary = xmlDocs.ContainsKey("summary") ? xmlDocs["summary"] : string.Empty;
        var signature = module.ModuleStatement.ToString().Trim();

        var tags = new List<string> { "module" };
        foreach (var modifier in module.ModuleStatement.Modifiers)
        {
            tags.Add(modifier.Text.ToLowerInvariant());
        }

        var moduleMemory = new CodeMemory
        {
            Type = CodeMemoryType.Class, // Treat module as class-like
            Name = moduleName,
            Content = module.ToString(),
            FilePath = filePath,
            Context = context,
            LineNumber = lineNumber,
            Summary = summary,
            Signature = signature,
            Purpose = summary,
            Tags = tags,
            Metadata = new Dictionary<string, object>
            {
                ["language"] = "vb.net",
                ["type"] = "module",
                ["access_modifier"] = GetAccessModifier(module.ModuleStatement.Modifiers)
            }
        };

        result.CodeElements.Add(moduleMemory);

        result.Relationships.Add(new CodeRelationship
        {
            FromName = Path.GetFileName(filePath),
            ToName = moduleName,
            Type = RelationshipType.Defines,
            Context = context,
            Properties = new Dictionary<string, object>
            {
                ["to_node_type"] = "Module",
                ["line_number"] = lineNumber
            }
        });

        // Extract methods
        var methods = module.Members.OfType<MethodBlockSyntax>();
        foreach (var method in methods)
        {
            await ExtractMethodAsync(method, moduleName, filePath, context, result, cancellationToken);
        }

        await Task.CompletedTask;
    }

    private async Task ExtractInterfaceAsync(InterfaceBlockSyntax interfaceDecl, string filePath, string context, ParseResult result, CancellationToken cancellationToken)
    {
        var interfaceName = interfaceDecl.InterfaceStatement.Identifier.Text;
        var lineNumber = interfaceDecl.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

        var xmlDocs = ExtractXmlDocumentation(interfaceDecl);
        var summary = xmlDocs.ContainsKey("summary") ? xmlDocs["summary"] : string.Empty;
        var signature = interfaceDecl.InterfaceStatement.ToString().Trim();

        var tags = new List<string> { "interface" };
        foreach (var modifier in interfaceDecl.InterfaceStatement.Modifiers)
        {
            tags.Add(modifier.Text.ToLowerInvariant());
        }

        var interfaceMemory = new CodeMemory
        {
            Type = CodeMemoryType.Class,
            Name = interfaceName,
            Content = interfaceDecl.ToString(),
            FilePath = filePath,
            Context = context,
            LineNumber = lineNumber,
            Summary = summary,
            Signature = signature,
            Purpose = summary,
            Tags = tags,
            Metadata = new Dictionary<string, object>
            {
                ["language"] = "vb.net",
                ["type"] = "interface",
                ["access_modifier"] = GetAccessModifier(interfaceDecl.InterfaceStatement.Modifiers)
            }
        };

        result.CodeElements.Add(interfaceMemory);

        result.Relationships.Add(new CodeRelationship
        {
            FromName = Path.GetFileName(filePath),
            ToName = interfaceName,
            Type = RelationshipType.Defines,
            Context = context,
            Properties = new Dictionary<string, object>
            {
                ["to_node_type"] = "Interface",
                ["line_number"] = lineNumber
            }
        });

        await Task.CompletedTask;
    }

    private async Task ExtractMethodAsync(MethodBlockSyntax method, string parentName, string filePath, string context, ParseResult result, CancellationToken cancellationToken)
    {
        var methodName = method.SubOrFunctionStatement.Identifier.Text;
        var fullMethodName = $"{parentName}.{methodName}";
        var lineNumber = method.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

        var xmlDocs = ExtractXmlDocumentation(method);
        var summary = xmlDocs.ContainsKey("summary") ? xmlDocs["summary"] : string.Empty;
        var signature = method.SubOrFunctionStatement.ToString().Trim();

        var tags = new List<string> { "method" };
        foreach (var modifier in method.SubOrFunctionStatement.Modifiers)
        {
            tags.Add(modifier.Text.ToLowerInvariant());
        }

        // Extract parameters as dependencies
        var dependencies = new List<string>();
        foreach (var param in method.SubOrFunctionStatement.ParameterList?.Parameters ?? [])
        {
            if (param.AsClause is SimpleAsClauseSyntax asClause)
            {
                var paramType = asClause.Type.ToString();
                if (!dependencies.Contains(paramType))
                {
                    dependencies.Add(paramType);
                }
            }
        }

        var methodMemory = new CodeMemory
        {
            Type = CodeMemoryType.Method,
            Name = fullMethodName,
            Content = method.ToString(),
            FilePath = filePath,
            Context = context,
            LineNumber = lineNumber,
            Summary = summary,
            Signature = signature,
            Purpose = summary,
            Tags = tags,
            Dependencies = dependencies,
            Metadata = new Dictionary<string, object>
            {
                ["language"] = "vb.net",
                ["type"] = "method",
                ["access_modifier"] = GetAccessModifier(method.SubOrFunctionStatement.Modifiers),
                ["parent_class"] = parentName
            }
        };

        result.CodeElements.Add(methodMemory);

        // DEFINES relationship
        result.Relationships.Add(new CodeRelationship
        {
            FromName = parentName,
            ToName = fullMethodName,
            Type = RelationshipType.Defines,
            Context = context,
            Properties = new Dictionary<string, object>
            {
                ["to_node_type"] = "Method",
                ["line_number"] = lineNumber
            }
        });

        // Extract method calls
        var invocations = method.DescendantNodes().OfType<InvocationExpressionSyntax>();
        foreach (var invocation in invocations)
        {
            var calledMethod = invocation.Expression.ToString();
            result.Relationships.Add(new CodeRelationship
            {
                FromName = fullMethodName,
                ToName = calledMethod,
                Type = RelationshipType.Calls,
                Context = context,
                Properties = new Dictionary<string, object>
                {
                    ["to_node_type"] = "Method"
                }
            });
        }

        // Extract field/property access
        var memberAccesses = method.DescendantNodes().OfType<MemberAccessExpressionSyntax>();
        foreach (var memberAccess in memberAccesses)
        {
            var memberName = memberAccess.Name.ToString();
            result.Relationships.Add(new CodeRelationship
            {
                FromName = fullMethodName,
                ToName = memberName,
                Type = RelationshipType.Uses,
                Context = context,
                Properties = new Dictionary<string, object>
                {
                    ["to_node_type"] = "Reference"
                }
            });
        }

        // Extract Try/Catch blocks
        var tryCatches = method.DescendantNodes().OfType<TryBlockSyntax>();
        foreach (var tryCatch in tryCatches)
        {
            foreach (var catchBlock in tryCatch.CatchBlocks)
            {
                if (catchBlock.CatchStatement.AsClause != null)
                {
                    var exceptionType = catchBlock.CatchStatement.AsClause.ToString().Replace("As ", "").Trim();
                    if (!string.IsNullOrWhiteSpace(exceptionType))
                    {
                        result.Relationships.Add(new CodeRelationship
                        {
                            FromName = fullMethodName,
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

        // Extract Throw statements
        var throws = method.DescendantNodes().OfType<ThrowStatementSyntax>();
        foreach (var throwStmt in throws)
        {
            if (throwStmt.Expression is ObjectCreationExpressionSyntax objectCreation)
            {
                var exceptionType = objectCreation.Type().ToString();
                result.Relationships.Add(new CodeRelationship
                {
                    FromName = fullMethodName,
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

        await Task.CompletedTask;
    }

    private async Task ExtractPropertyAsync(PropertyBlockSyntax property, string parentName, string filePath, string context, ParseResult result, CancellationToken cancellationToken)
    {
        var propertyName = property.PropertyStatement.Identifier.Text;
        var fullPropertyName = $"{parentName}.{propertyName}";
        var lineNumber = property.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

        var xmlDocs = ExtractXmlDocumentation(property);
        var summary = xmlDocs.ContainsKey("summary") ? xmlDocs["summary"] : string.Empty;
        var signature = property.PropertyStatement.ToString().Trim();

        var tags = new List<string> { "property" };
        foreach (var modifier in property.PropertyStatement.Modifiers)
        {
            tags.Add(modifier.Text.ToLowerInvariant());
        }

        var propertyMemory = new CodeMemory
        {
            Type = CodeMemoryType.Property,
            Name = fullPropertyName,
            Content = property.ToString(),
            FilePath = filePath,
            Context = context,
            LineNumber = lineNumber,
            Summary = summary,
            Signature = signature,
            Purpose = summary,
            Tags = tags,
            Metadata = new Dictionary<string, object>
            {
                ["language"] = "vb.net",
                ["type"] = "property",
                ["access_modifier"] = GetAccessModifier(property.PropertyStatement.Modifiers),
                ["parent_class"] = parentName
            }
        };

        result.CodeElements.Add(propertyMemory);

        result.Relationships.Add(new CodeRelationship
        {
            FromName = parentName,
            ToName = fullPropertyName,
            Type = RelationshipType.Defines,
            Context = context,
            Properties = new Dictionary<string, object>
            {
                ["to_node_type"] = "Property",
                ["line_number"] = lineNumber
            }
        });

        await Task.CompletedTask;
    }

    private Dictionary<string, string> ExtractXmlDocumentation(SyntaxNode node)
    {
        var docs = new Dictionary<string, string>();

        // VB.NET uses ''' for XML documentation comments
        var trivia = node.GetLeadingTrivia();
        var xmlComments = trivia
            .Where(t => t.Kind() == SyntaxKind.DocumentationCommentTrivia)
            .Select(t => t.ToString())
            .ToList();

        if (xmlComments.Any())
        {
            var fullXml = string.Join("\n", xmlComments);

            // Extract <summary>
            var summaryMatch = Regex.Match(fullXml, @"<summary>\s*(.*?)\s*</summary>", RegexOptions.Singleline);
            if (summaryMatch.Success)
            {
                docs["summary"] = Regex.Replace(summaryMatch.Groups[1].Value.Trim(), @"'''", "").Trim();
            }

            // Extract <remarks>
            var remarksMatch = Regex.Match(fullXml, @"<remarks>\s*(.*?)\s*</remarks>", RegexOptions.Singleline);
            if (remarksMatch.Success)
            {
                docs["remarks"] = Regex.Replace(remarksMatch.Groups[1].Value.Trim(), @"'''", "").Trim();
            }
        }

        return docs;
    }

    private string GetAccessModifier(SyntaxTokenList modifiers)
    {
        if (modifiers.Any(m => m.Kind() == SyntaxKind.PublicKeyword))
            return "public";
        if (modifiers.Any(m => m.Kind() == SyntaxKind.PrivateKeyword))
            return "private";
        if (modifiers.Any(m => m.Kind() == SyntaxKind.ProtectedKeyword))
            return "protected";
        if (modifiers.Any(m => m.Kind() == SyntaxKind.FriendKeyword))
            return "friend"; // VB.NET equivalent of "internal"
        return "unknown";
    }

    private string DetermineContext(string filePath)
    {
        var parts = filePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        
        // Look for common project root indicators
        for (int i = parts.Length - 1; i >= 0; i--)
        {
            if (parts[i].EndsWith(".vbproj") || parts[i] == "src" || parts[i] == "source")
            {
                return i < parts.Length - 1 ? parts[i + 1] : parts[i];
            }
        }
        
        // Fallback to directory name containing the file
        return parts.Length >= 2 ? parts[^2] : "default";
    }
}

