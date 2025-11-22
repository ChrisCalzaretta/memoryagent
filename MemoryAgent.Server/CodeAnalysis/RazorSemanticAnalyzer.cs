using System.Text.RegularExpressions;
using MemoryAgent.Server.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MemoryAgent.Server.CodeAnalysis;

/// <summary>
/// Semantic analyzer for Razor/CSHTML files
/// Extracts ASP.NET semantic patterns from Razor Pages
/// </summary>
public class RazorSemanticAnalyzer
{
    private readonly ILogger<RazorSemanticAnalyzer> _logger;

    public RazorSemanticAnalyzer(ILogger<RazorSemanticAnalyzer> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Analyze Razor file and extract semantic patterns
    /// </summary>
    public void AnalyzeRazorFile(string content, string filePath, string? context, ParseResult result)
    {
        try
        {
            var fileName = Path.GetFileNameWithoutExtension(filePath);
            
            // Extract @page directive and create Endpoint
            ExtractPageDirective(content, fileName, filePath, context, result);
            
            // Extract @inject directives
            ExtractInjectDirectives(content, fileName, filePath, context, result);
            
            // Extract @attribute [Authorize]
            ExtractAuthorizeAttributes(content, fileName, context, result);
            
            // Extract @code blocks and analyze for EF queries
            ExtractAndAnalyzeCodeBlocks(content, fileName, filePath, context, result);
            
            // Extract form handlers (OnGet, OnPost, etc.)
            ExtractFormHandlers(content, fileName, filePath, context, result);
            
            _logger.LogInformation("Completed semantic analysis for Razor file: {FileName}", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in semantic analysis for Razor file: {FilePath}", filePath);
        }
    }

    /// <summary>
    /// Extract @page directive and create Endpoint node
    /// </summary>
    private void ExtractPageDirective(string content, string fileName, string filePath, string? context, ParseResult result)
    {
        // @page "/projects/{id}"
        // @page (no route - uses file path)
        var pageMatch = Regex.Match(content, @"@page\s*(?:""([^""]+)""|$)", RegexOptions.Multiline);
        if (!pageMatch.Success)
            return;
        
        var route = pageMatch.Groups[1].Success 
            ? pageMatch.Groups[1].Value 
            : ConvertFilePathToRoute(filePath);
        
        var lineNumber = GetLineNumber(content, pageMatch.Index);
        
        // Create Endpoint node for GET (default for Razor Pages)
        var getEndpoint = new CodeMemory
        {
            Type = CodeMemoryType.Other,
            Name = $"Endpoint(GET {route})",
            Content = $"@page \"{route}\"",
            FilePath = filePath,
            Context = context ?? "default",
            LineNumber = lineNumber,
            Metadata = new Dictionary<string, object>
            {
                ["chunk_type"] = "endpoint",
                ["route"] = route,
                ["http_method"] = "GET",
                ["page_model"] = fileName,
                ["framework"] = "aspnet-core",
                ["layer"] = "UI",
                ["is_razor_page"] = true
            }
        };
        result.CodeElements.Add(getEndpoint);
        
        // EXPOSES relationship
        result.Relationships.Add(new CodeRelationship
        {
            FromName = $"Endpoint(GET {route})",
            ToName = fileName,
            Type = RelationshipType.Exposes,
            Context = context ?? "default",
            Properties = new Dictionary<string, object>
            {
                ["http_method"] = "GET",
                ["page_type"] = "RazorPage"
            }
        });
    }

    /// <summary>
    /// Extract @inject directives and create INJECTS relationships
    /// </summary>
    private void ExtractInjectDirectives(string content, string fileName, string filePath, string? context, ParseResult result)
    {
        // @inject IUserService UserService
        // @inject ILogger<Index> Logger
        var injectMatches = Regex.Matches(content, @"@inject\s+([^\s]+)\s+([^\r\n]+)", RegexOptions.Multiline);
        
        foreach (Match match in injectMatches)
        {
            var serviceType = match.Groups[1].Value.Trim();
            var variableName = match.Groups[2].Value.Trim();
            var lineNumber = GetLineNumber(content, match.Index);
            
            // Create injection chunk
            var injectionChunk = new CodeMemory
            {
                Type = CodeMemoryType.Other,
                Name = $"Inject: {serviceType}",
                Content = match.Value,
                FilePath = filePath,
                Context = context ?? "default",
                LineNumber = lineNumber,
                Metadata = new Dictionary<string, object>
                {
                    ["chunk_type"] = "dependency_injection",
                    ["service_type"] = serviceType,
                    ["variable_name"] = variableName,
                    ["injection_method"] = "@inject",
                    ["framework"] = "aspnet-core",
                    ["layer"] = "UI"
                }
            };
            result.CodeElements.Add(injectionChunk);
            
            // INJECTS relationship
            result.Relationships.Add(new CodeRelationship
            {
                FromName = fileName,
                ToName = serviceType,
                Type = RelationshipType.Injects,
                Context = context ?? "default",
                Properties = new Dictionary<string, object>
                {
                    ["injection_method"] = "@inject"
                }
            });
        }
    }

    /// <summary>
    /// Extract @attribute [Authorize] and create AUTHORIZES relationships
    /// </summary>
    private void ExtractAuthorizeAttributes(string content, string fileName, string? context, ParseResult result)
    {
        // @attribute [Authorize]
        // @attribute [Authorize(Roles = "Admin")]
        // @attribute [Authorize(Policy = "RequireAdmin")]
        var authorizeMatches = Regex.Matches(content, 
            @"@attribute\s+\[Authorize(?:\([^\]]*\))?\]", 
            RegexOptions.Multiline);
        
        foreach (Match match in authorizeMatches)
        {
            var authText = match.Value;
            var lineNumber = GetLineNumber(content, match.Index);
            
            // Extract roles
            var rolesMatch = Regex.Match(authText, @"Roles\s*=\s*""([^""]+)""");
            if (rolesMatch.Success)
            {
                var roles = rolesMatch.Groups[1].Value.Split(',').Select(r => r.Trim());
                foreach (var role in roles)
                {
                    result.Relationships.Add(new CodeRelationship
                    {
                        FromName = fileName,
                        ToName = $"Role({role})",
                        Type = RelationshipType.Authorizes,
                        Context = context ?? "default"
                    });
                }
            }
            
            // Extract policy
            var policyMatch = Regex.Match(authText, @"Policy\s*=\s*""([^""]+)""");
            if (policyMatch.Success)
            {
                var policy = policyMatch.Groups[1].Value;
                result.Relationships.Add(new CodeRelationship
                {
                    FromName = fileName,
                    ToName = $"Policy({policy})",
                    Type = RelationshipType.RequiresPolicy,
                    Context = context ?? "default"
                });
            }
            
            // Generic authorization (no specific role or policy)
            if (!rolesMatch.Success && !policyMatch.Success)
            {
                result.Relationships.Add(new CodeRelationship
                {
                    FromName = fileName,
                    ToName = "AuthenticatedUser",
                    Type = RelationshipType.Authorizes,
                    Context = context ?? "default"
                });
            }
        }
    }

    /// <summary>
    /// Extract @code blocks and analyze for EF queries
    /// </summary>
    private void ExtractAndAnalyzeCodeBlocks(string content, string fileName, string filePath, string? context, ParseResult result)
    {
        // Match @code { ... } blocks
        var codeBlockMatches = Regex.Matches(content, @"@code\s*\{", RegexOptions.Multiline);
        
        foreach (Match match in codeBlockMatches)
        {
            var startIndex = match.Index + match.Length;
            var braceCount = 1;
            var endIndex = startIndex;
            
            // Find matching closing brace
            for (int i = startIndex; i < content.Length && braceCount > 0; i++)
            {
                if (content[i] == '{') braceCount++;
                if (content[i] == '}') braceCount--;
                if (braceCount == 0) endIndex = i;
            }
            
            if (endIndex > startIndex)
            {
                var codeBlock = content.Substring(startIndex, endIndex - startIndex);
                
                // Parse the C# code in the @code block
                AnalyzeCodeBlock(codeBlock, fileName, filePath, context, result);
            }
        }
    }

    /// <summary>
    /// Analyze C# code block for EF queries and other patterns
    /// </summary>
    private void AnalyzeCodeBlock(string codeBlock, string fileName, string filePath, string? context, ParseResult result)
    {
        try
        {
            // Parse as C# code
            var tree = CSharpSyntaxTree.ParseText(codeBlock);
            var root = tree.GetRoot();
            
            // Extract methods (OnGet, OnPost, etc.)
            var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();
            
            foreach (var method in methods)
            {
                var methodName = method.Identifier.Text;
                
                // Detect Razor Page handlers (OnGet, OnPost, OnPostDelete, etc.)
                if (IsRazorPageHandler(methodName))
                {
                    var httpMethod = DetermineHttpMethod(methodName);
                    
                    // Extract EF queries within this method
                    ExtractEFQueriesFromMethod(method, $"{fileName}.{methodName}", context, result);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not parse @code block as C# in {FileName}", fileName);
        }
    }

    /// <summary>
    /// Extract form handlers (OnGet, OnPost, etc.) from @code blocks
    /// </summary>
    private void ExtractFormHandlers(string content, string fileName, string filePath, string? context, ParseResult result)
    {
        // Match method declarations in @code blocks
        var handlerPattern = @"public\s+(?:async\s+)?(?:Task<?(?:IActionResult|PageResult)?>?|IActionResult|PageResult|void)\s+(On(?:Get|Post|Put|Delete|Patch)\w*)\s*\(";
        var handlerMatches = Regex.Matches(content, handlerPattern, RegexOptions.Multiline);
        
        foreach (Match match in handlerMatches)
        {
            var handlerName = match.Groups[1].Value;
            var httpMethod = DetermineHttpMethod(handlerName);
            var lineNumber = GetLineNumber(content, match.Index);
            
            // Get the route from @page directive
            var pageMatch = Regex.Match(content, @"@page\s*(?:""([^""]+)""|$)");
            var route = pageMatch.Success && pageMatch.Groups[1].Success
                ? pageMatch.Groups[1].Value
                : ConvertFilePathToRoute(filePath);
            
            // For POST/PUT/DELETE handlers, create additional Endpoint nodes
            if (httpMethod != "GET")
            {
                var endpoint = new CodeMemory
                {
                    Type = CodeMemoryType.Other,
                    Name = $"Endpoint({httpMethod} {route})",
                    Content = $"{httpMethod} {route} -> {handlerName}",
                    FilePath = filePath,
                    Context = context ?? "default",
                    LineNumber = lineNumber,
                    Metadata = new Dictionary<string, object>
                    {
                        ["chunk_type"] = "endpoint",
                        ["route"] = route,
                        ["http_method"] = httpMethod,
                        ["handler"] = handlerName,
                        ["page_model"] = fileName,
                        ["framework"] = "aspnet-core",
                        ["layer"] = "UI",
                        ["is_razor_page"] = true
                    }
                };
                result.CodeElements.Add(endpoint);
                
                // EXPOSES relationship
                result.Relationships.Add(new CodeRelationship
                {
                    FromName = $"Endpoint({httpMethod} {route})",
                    ToName = $"{fileName}.{handlerName}",
                    Type = RelationshipType.Exposes,
                    Context = context ?? "default",
                    Properties = new Dictionary<string, object>
                    {
                        ["http_method"] = httpMethod,
                        ["handler_type"] = "RazorPageHandler"
                    }
                });
            }
        }
    }

    /// <summary>
    /// Extract EF queries from a method
    /// </summary>
    private void ExtractEFQueriesFromMethod(MethodDeclarationSyntax method, string fullMethodName, string? context, ParseResult result)
    {
        var invocations = method.DescendantNodes().OfType<InvocationExpressionSyntax>();
        
        var efOperations = new List<string>();
        var includedEntities = new List<string>();
        var queriedEntity = string.Empty;
        
        foreach (var invocation in invocations)
        {
            var methodName = GetMethodName(invocation);
            
            if (IsEFQueryOperation(methodName))
            {
                efOperations.Add(methodName);
                
                // Extract Include/ThenInclude
                if (methodName == "Include" || methodName == "ThenInclude")
                {
                    var entity = ExtractIncludedEntity(invocation.ToString());
                    if (!string.IsNullOrEmpty(entity))
                    {
                        includedEntities.Add(entity);
                    }
                }
            }
            
            // Detect DbSet/DbContext access
            if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
            {
                var memberName = memberAccess.Name.ToString();
                if (char.IsUpper(memberName[0]) && !memberName.Contains("Async"))
                {
                    queriedEntity = memberName;
                }
            }
        }
        
        // Create relationships if EF query detected
        if (efOperations.Any() && !string.IsNullOrEmpty(queriedEntity))
        {
            // QUERIES relationship
            result.Relationships.Add(new CodeRelationship
            {
                FromName = fullMethodName,
                ToName = $"{queriedEntity} (Entity)",
                Type = RelationshipType.Queries,
                Context = context ?? "default",
                Properties = new Dictionary<string, object>
                {
                    ["operation"] = "Read",
                    ["source"] = "RazorPage"
                }
            });
            
            // INCLUDES relationships
            foreach (var entity in includedEntities)
            {
                result.Relationships.Add(new CodeRelationship
                {
                    FromName = fullMethodName,
                    ToName = $"{entity} (Entity)",
                    Type = RelationshipType.Includes,
                    Context = context ?? "default",
                    Properties = new Dictionary<string, object>
                    {
                        ["eager_load"] = true
                    }
                });
            }
        }
    }

    #region Helper Methods

    private bool IsRazorPageHandler(string methodName)
    {
        return methodName.StartsWith("On") && 
               (methodName.StartsWith("OnGet") || 
                methodName.StartsWith("OnPost") || 
                methodName.StartsWith("OnPut") || 
                methodName.StartsWith("OnDelete") ||
                methodName.StartsWith("OnPatch"));
    }

    private string DetermineHttpMethod(string handlerName)
    {
        if (handlerName.StartsWith("OnGet")) return "GET";
        if (handlerName.StartsWith("OnPost")) return "POST";
        if (handlerName.StartsWith("OnPut")) return "PUT";
        if (handlerName.StartsWith("OnDelete")) return "DELETE";
        if (handlerName.StartsWith("OnPatch")) return "PATCH";
        return "GET"; // Default
    }

    private string ConvertFilePathToRoute(string filePath)
    {
        // Convert file path to route
        // e.g., Pages/Projects/Details.cshtml -> /projects/details
        var fileName = Path.GetFileNameWithoutExtension(filePath);
        
        // Handle Index pages
        if (fileName.Equals("Index", StringComparison.OrdinalIgnoreCase))
        {
            fileName = "";
        }
        
        // Try to extract from path structure
        var parts = filePath.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
        var pagesIndex = Array.FindIndex(parts, p => p.Equals("Pages", StringComparison.OrdinalIgnoreCase));
        
        if (pagesIndex >= 0 && pagesIndex < parts.Length - 1)
        {
            var routeParts = parts.Skip(pagesIndex + 1).Take(parts.Length - pagesIndex - 2).ToList();
            if (!string.IsNullOrEmpty(fileName))
            {
                routeParts.Add(fileName);
            }
            return "/" + string.Join("/", routeParts).ToLowerInvariant();
        }
        
        return "/" + fileName.ToLowerInvariant();
    }

    private int GetLineNumber(string content, int charIndex)
    {
        return content.Substring(0, Math.Min(charIndex, content.Length)).Count(c => c == '\n') + 1;
    }

    private string GetMethodName(InvocationExpressionSyntax invocation)
    {
        return invocation.Expression switch
        {
            IdentifierNameSyntax identifierName => identifierName.Identifier.Text,
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.Text,
            GenericNameSyntax genericName => genericName.Identifier.Text,
            _ => invocation.Expression.ToString()
        };
    }

    private bool IsEFQueryOperation(string methodName)
    {
        var efOperations = new[]
        {
            "Where", "Select", "OrderBy", "OrderByDescending", "ThenBy", "ThenByDescending",
            "GroupBy", "Join", "SelectMany", "Distinct", "Skip", "Take",
            "First", "FirstOrDefault", "Single", "SingleOrDefault", "Any", "Count",
            "Include", "ThenInclude", "AsNoTracking", "ToList", "ToListAsync",
            "FirstAsync", "FirstOrDefaultAsync", "SingleAsync", "SingleOrDefaultAsync"
        };
        
        return efOperations.Contains(methodName);
    }

    private string ExtractIncludedEntity(string invocationText)
    {
        // Extract entity from Include(u => u.Profile) or ThenInclude(p => p.Address)
        var match = Regex.Match(invocationText, @"\w+\s*=>\s*\w+\.(\w+)");
        return match.Success ? match.Groups[1].Value : string.Empty;
    }

    #endregion
}

