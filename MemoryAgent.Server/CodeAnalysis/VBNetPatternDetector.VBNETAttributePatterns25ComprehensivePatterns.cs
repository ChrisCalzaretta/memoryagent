using MemoryAgent.Server.Models;
using System.Text.RegularExpressions;

namespace MemoryAgent.Server.CodeAnalysis;

/// <summary>
/// VB.NET Attribute Patterns (25 Comprehensive Patterns)
/// </summary>
public partial class VBNetPatternDetector : IPatternDetector
{
    #region VB.NET Attribute Patterns (25 Comprehensive Patterns)

    // ==================== ROUTING ATTRIBUTES ====================
    
    private List<CodePattern> DetectRoutingAttributes(string[] lines, string filePath, string? context)
    {
        var patterns = new List<CodePattern>();

        // <HttpGet>, <HttpPost>, <HttpPut>, <HttpDelete>, <HttpPatch>
        for (int i = 0; i < lines.Length; i++)
        {
            var match = Regex.Match(lines[i], @"<Http(Get|Post|Put|Delete|Patch)(\(""[^""]*""\))?>", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var httpMethod = match.Groups[1].Value;
                var route = match.Groups[2].Success ? match.Groups[2].Value : "";
                
                patterns.Add(CreatePattern(
                    name: $"VBNet_Http{httpMethod}",
                    type: PatternType.ApiDesign,
                    category: PatternCategory.Operational,
                    implementation: $"<Http{httpMethod}> attribute",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 3),
                    bestPractice: $"Use <Http{httpMethod}> to define {httpMethod} endpoint, specify route template for RESTful design",
                    azureUrl: "https://learn.microsoft.com/en-us/aspnet/core/mvc/controllers/routing",
                    context: context
                ));
            }
        }

        // <Route("...")>
        for (int i = 0; i < lines.Length; i++)
        {
            var match = Regex.Match(lines[i], @"<Route\(""([^""]+)""\)>", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var routeTemplate = match.Groups[1].Value;
                
                patterns.Add(CreatePattern(
                    name: "VBNet_Route",
                    type: PatternType.ApiDesign,
                    category: PatternCategory.Operational,
                    implementation: $"<Route(\"{routeTemplate}\")>",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 3),
                    bestPractice: "Use <Route> to define controller/action route template, use route parameters like {{id}}, apply to controllers and actions",
                    azureUrl: "https://learn.microsoft.com/en-us/aspnet/core/mvc/controllers/routing",
                    context: context
                ));
            }
        }

        // <ApiController>
        for (int i = 0; i < lines.Length; i++)
        {
            if (Regex.IsMatch(lines[i], @"<ApiController>", RegexOptions.IgnoreCase))
            {
                patterns.Add(CreatePattern(
                    name: "VBNet_ApiController",
                    type: PatternType.ApiDesign,
                    category: PatternCategory.Operational,
                    implementation: "<ApiController> attribute",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 3),
                    bestPractice: "Use <ApiController> for automatic model validation, binding source inference, and API-specific behaviors",
                    azureUrl: "https://learn.microsoft.com/en-us/aspnet/core/web-api/",
                    context: context
                ));
            }
        }

        // <FromBody>, <FromQuery>, <FromRoute>, <FromHeader>, <FromServices>
        for (int i = 0; i < lines.Length; i++)
        {
            var match = Regex.Match(lines[i], @"<From(Body|Query|Route|Header|Services|Form)>", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var bindingSource = match.Groups[1].Value;
                
                patterns.Add(CreatePattern(
                    name: $"VBNet_From{bindingSource}",
                    type: PatternType.ApiDesign,
                    category: PatternCategory.Operational,
                    implementation: $"<From{bindingSource}> attribute",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 3),
                    bestPractice: $"Use <From{bindingSource}> to specify parameter binding source explicitly, improves API documentation and validation",
                    azureUrl: "https://learn.microsoft.com/en-us/aspnet/core/mvc/models/model-binding",
                    context: context
                ));
            }
        }

        return patterns;
    }

    // ==================== AUTHORIZATION ATTRIBUTES ====================
    
    private List<CodePattern> DetectAuthorizationAttributes(string[] lines, string filePath, string? context)
    {
        var patterns = new List<CodePattern>();

        // <Authorize>
        for (int i = 0; i < lines.Length; i++)
        {
            var match = Regex.Match(lines[i], @"<Authorize(\([^)]*\))?>", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var parameters = match.Groups[1].Success ? match.Groups[1].Value : "";
                var hasRoles = parameters.Contains("Roles");
                var hasPolicy = parameters.Contains("Policy");
                
                patterns.Add(CreatePattern(
                    name: "VBNet_Authorize",
                    type: PatternType.Security,
                    category: PatternCategory.Security,
                    implementation: $"<Authorize{parameters}> attribute",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 3),
                    bestPractice: "Use <Authorize> to protect controllers/actions, specify Roles or Policy for fine-grained access control",
                    azureUrl: "https://learn.microsoft.com/en-us/aspnet/core/security/authorization/simple",
                    context: context
                ));
            }
        }

        // <AllowAnonymous>
        for (int i = 0; i < lines.Length; i++)
        {
            if (Regex.IsMatch(lines[i], @"<AllowAnonymous>", RegexOptions.IgnoreCase))
            {
                patterns.Add(CreatePattern(
                    name: "VBNet_AllowAnonymous",
                    type: PatternType.Security,
                    category: PatternCategory.Security,
                    implementation: "<AllowAnonymous> attribute",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 3),
                    bestPractice: "Use <AllowAnonymous> to bypass authorization on specific actions within authorized controllers",
                    azureUrl: "https://learn.microsoft.com/en-us/aspnet/core/security/authorization/simple",
                    context: context
                ));
            }
        }

        // <ValidateAntiForgeryToken>
        for (int i = 0; i < lines.Length; i++)
        {
            if (Regex.IsMatch(lines[i], @"<ValidateAntiForgeryToken>", RegexOptions.IgnoreCase))
            {
                patterns.Add(CreatePattern(
                    name: "VBNet_ValidateAntiForgeryToken",
                    type: PatternType.Security,
                    category: PatternCategory.Security,
                    implementation: "<ValidateAntiForgeryToken> attribute",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 3),
                    bestPractice: "ALWAYS use <ValidateAntiForgeryToken> on POST/PUT/DELETE actions to prevent CSRF attacks",
                    azureUrl: "https://learn.microsoft.com/en-us/aspnet/core/security/anti-request-forgery",
                    context: context
                ));
            }
        }

        return patterns;
    }

    // ==================== VALIDATION ATTRIBUTES ====================
    
    private List<CodePattern> DetectValidationAttributes(string[] lines, string filePath, string? context)
    {
        var patterns = new List<CodePattern>();

        // <Required>
        for (int i = 0; i < lines.Length; i++)
        {
            if (Regex.IsMatch(lines[i], @"<Required", RegexOptions.IgnoreCase))
            {
                patterns.Add(CreatePattern(
                    name: "VBNet_Required",
                    type: PatternType.Validation,
                    category: PatternCategory.Operational,
                    implementation: "<Required> attribute",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 3),
                    bestPractice: "Use <Required> for mandatory properties, provide custom error message for better UX",
                    azureUrl: "https://learn.microsoft.com/en-us/dotnet/api/system.componentmodel.dataannotations.requiredattribute",
                    context: context
                ));
            }
        }

        // <StringLength>, <MaxLength>, <MinLength>
        for (int i = 0; i < lines.Length; i++)
        {
            var match = Regex.Match(lines[i], @"<(StringLength|MaxLength|MinLength)\((\d+)", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var attrName = match.Groups[1].Value;
                var length = match.Groups[2].Value;
                
                patterns.Add(CreatePattern(
                    name: $"VBNet_{attrName}",
                    type: PatternType.Validation,
                    category: PatternCategory.Operational,
                    implementation: $"<{attrName}({length})> attribute",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 3),
                    bestPractice: $"Use <{attrName}> to enforce string length constraints, prevents database truncation and improves data quality",
                    azureUrl: "https://learn.microsoft.com/en-us/dotnet/api/system.componentmodel.dataannotations",
                    context: context
                ));
            }
        }

        // <Range>, <EmailAddress>, <Phone>, <Url>, <CreditCard>
        for (int i = 0; i < lines.Length; i++)
        {
            var match = Regex.Match(lines[i], @"<(Range|EmailAddress|Phone|Url|CreditCard|RegularExpression)", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var attrName = match.Groups[1].Value;
                
                patterns.Add(CreatePattern(
                    name: $"VBNet_{attrName}",
                    type: PatternType.Validation,
                    category: PatternCategory.Operational,
                    implementation: $"<{attrName}> attribute",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 3),
                    bestPractice: $"Use <{attrName}> for automatic validation, client-side and server-side validation enabled",
                    azureUrl: "https://learn.microsoft.com/en-us/dotnet/api/system.componentmodel.dataannotations",
                    context: context
                ));
            }
        }

        // <Compare>
        for (int i = 0; i < lines.Length; i++)
        {
            var match = Regex.Match(lines[i], @"<Compare\(""([^""]+)""\)>", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var compareProperty = match.Groups[1].Value;
                
                patterns.Add(CreatePattern(
                    name: "VBNet_Compare",
                    type: PatternType.Validation,
                    category: PatternCategory.Operational,
                    implementation: $"<Compare(\"{compareProperty}\")>",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 3),
                    bestPractice: "Use <Compare> for password confirmation, email verification, ensure two fields match",
                    azureUrl: "https://learn.microsoft.com/en-us/dotnet/api/system.componentmodel.dataannotations.compareattribute",
                    context: context
                ));
            }
        }

        return patterns;
    }

    // ==================== BLAZOR COMPONENT ATTRIBUTES ====================
    
    private List<CodePattern> DetectBlazorComponentAttributes(string[] lines, string filePath, string? context)
    {
        var patterns = new List<CodePattern>();

        // <Parameter>
        for (int i = 0; i < lines.Length; i++)
        {
            if (Regex.IsMatch(lines[i], @"<Parameter>", RegexOptions.IgnoreCase))
            {
                patterns.Add(CreatePattern(
                    name: "VBNet_ComponentParameter",
                    type: PatternType.StateManagement,
                    category: PatternCategory.StateManagement,
                    implementation: "<Parameter> attribute",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 3),
                    bestPractice: "Use <Parameter> for component input properties, make parameters immutable, use EventCallback for output",
                    azureUrl: "https://learn.microsoft.com/en-us/aspnet/core/blazor/components/",
                    context: context
                ));
            }
        }

        // <CascadingParameter>
        for (int i = 0; i < lines.Length; i++)
        {
            if (Regex.IsMatch(lines[i], @"<CascadingParameter", RegexOptions.IgnoreCase))
            {
                patterns.Add(CreatePattern(
                    name: "VBNet_CascadingParameter",
                    type: PatternType.StateManagement,
                    category: PatternCategory.StateManagement,
                    implementation: "<CascadingParameter> attribute",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 3),
                    bestPractice: "Use <CascadingParameter> for implicit parameters from parent components, avoid overuse (prefer DI for services)",
                    azureUrl: "https://learn.microsoft.com/en-us/aspnet/core/blazor/components/cascading-values-and-parameters",
                    context: context
                ));
            }
        }

        // <Inject>
        for (int i = 0; i < lines.Length; i++)
        {
            if (Regex.IsMatch(lines[i], @"<Inject>", RegexOptions.IgnoreCase))
            {
                patterns.Add(CreatePattern(
                    name: "VBNet_Inject",
                    type: PatternType.DependencyInjection,
                    category: PatternCategory.Operational,
                    implementation: "<Inject> attribute",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 3),
                    bestPractice: "Use <Inject> for dependency injection in Blazor components, register services in Program.vb",
                    azureUrl: "https://learn.microsoft.com/en-us/aspnet/core/blazor/fundamentals/dependency-injection",
                    context: context
                ));
            }
        }

        // <SupplyParameterFromQuery>
        for (int i = 0; i < lines.Length; i++)
        {
            if (Regex.IsMatch(lines[i], @"<SupplyParameterFromQuery", RegexOptions.IgnoreCase))
            {
                patterns.Add(CreatePattern(
                    name: "VBNet_SupplyParameterFromQuery",
                    type: PatternType.StateManagement,
                    category: PatternCategory.StateManagement,
                    implementation: "<SupplyParameterFromQuery> attribute",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 3),
                    bestPractice: "Use <SupplyParameterFromQuery> for query string parameters in Blazor, validate values, never put sensitive data in URLs",
                    azureUrl: "https://learn.microsoft.com/en-us/aspnet/core/blazor/fundamentals/routing",
                    context: context
                ));
            }
        }

        return patterns;
    }

    // ==================== CACHING ATTRIBUTES ====================
    
    private List<CodePattern> DetectCachingAttributes(string[] lines, string filePath, string? context)
    {
        var patterns = new List<CodePattern>();

        // <ResponseCache>
        for (int i = 0; i < lines.Length; i++)
        {
            var match = Regex.Match(lines[i], @"<ResponseCache", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var hasDuration = lines[i].Contains("Duration");
                var hasVaryByHeader = lines[i].Contains("VaryByHeader");
                
                patterns.Add(CreatePattern(
                    name: "VBNet_ResponseCache",
                    type: PatternType.Caching,
                    category: PatternCategory.Operational,
                    implementation: "<ResponseCache> attribute",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 3),
                    bestPractice: "Use <ResponseCache> for HTTP caching headers, set Duration, VaryByHeader, CacheProfileName for reusability",
                    azureUrl: "https://learn.microsoft.com/en-us/aspnet/core/performance/caching/response",
                    context: context
                ));
            }
        }

        // <OutputCache> (.NET 7+)
        for (int i = 0; i < lines.Length; i++)
        {
            var match = Regex.Match(lines[i], @"<OutputCache", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                patterns.Add(CreatePattern(
                    name: "VBNet_OutputCache",
                    type: PatternType.Caching,
                    category: PatternCategory.Operational,
                    implementation: "<OutputCache> attribute",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 3),
                    bestPractice: "Use <OutputCache> for server-side output caching (.NET 7+), configure policies, use VaryByQuery for dynamic content",
                    azureUrl: "https://learn.microsoft.com/en-us/aspnet/core/performance/caching/output",
                    context: context
                ));
            }
        }

        return patterns;
    }

    
    #endregion
}
