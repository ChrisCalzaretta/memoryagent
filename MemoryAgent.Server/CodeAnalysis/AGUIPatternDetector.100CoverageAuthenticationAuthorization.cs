using MemoryAgent.Server.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text.RegularExpressions;

namespace MemoryAgent.Server.CodeAnalysis;

/// <summary>
/// 100% Coverage - Authentication & Authorization
/// </summary>
public partial class AGUIPatternDetector
{
    #region 100% Coverage - Authentication & Authorization

    private List<CodePattern> DetectAuthentication(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: JWT authentication for AG-UI
        if ((sourceCode.Contains("JWT") || sourceCode.Contains("JwtBearer")) &&
            (sourceCode.Contains("AG-UI") || sourceCode.Contains("agent") || sourceCode.Contains("api")))
        {
            patterns.Add(CreatePattern(
                name: "AGUI_JWTAuthentication",
                type: PatternType.Security,
                category: PatternCategory.Security,
                implementation: "JWT authentication for AG-UI endpoints",
                filePath: filePath,
                lineNumber: 1,
                content: "// JWT authentication detected",
                bestPractice: "Secure AG-UI endpoints with JWT authentication to verify user identity before agent interactions.",
                azureUrl: "https://learn.microsoft.com/en-us/aspnet/core/security/authentication/",
                context: context,
                confidence: 0.88f,
                metadata: new Dictionary<string, object>
                {
                    ["auth_type"] = "JWT",
                    ["endpoints"] = "AG-UI MapAGUI"
                }
            ));
        }

        // Pattern: Authorization policies
        if ((sourceCode.Contains("AuthorizeAttribute") || sourceCode.Contains("[Authorize") || sourceCode.Contains("Policy")) &&
            (sourceCode.Contains("agent") || sourceCode.Contains("tool")))
        {
            patterns.Add(CreatePattern(
                name: "AGUI_Authorization",
                type: PatternType.Security,
                category: PatternCategory.Security,
                implementation: "Authorization policies for AG-UI",
                filePath: filePath,
                lineNumber: 1,
                content: "// Authorization detected",
                bestPractice: "Implement authorization policies to control which users can access specific AG-UI agents or tools.",
                azureUrl: "https://learn.microsoft.com/en-us/aspnet/core/security/authorization/",
                context: context,
                confidence: 0.85f,
                metadata: new Dictionary<string, object>
                {
                    ["pattern"] = "Authorization",
                    ["scope"] = new[] { "Agent access", "Tool execution", "State modification" }
                }
            ));
        }

        // Pattern: API Key authentication
        if ((sourceCode.Contains("ApiKey") || sourceCode.Contains("API_KEY")) &&
            (sourceCode.Contains("agent") || sourceCode.Contains("AG-UI")))
        {
            patterns.Add(CreatePattern(
                name: "AGUI_ApiKeyAuth",
                type: PatternType.Security,
                category: PatternCategory.Security,
                implementation: "API Key authentication for AG-UI",
                filePath: filePath,
                lineNumber: 1,
                content: "// API Key authentication detected",
                bestPractice: "Use API keys for service-to-service AG-UI authentication, stored securely in Azure Key Vault.",
                azureUrl: "https://learn.microsoft.com/en-us/azure/key-vault/",
                context: context,
                confidence: 0.83f,
                metadata: new Dictionary<string, object>
                {
                    ["auth_type"] = "API Key",
                    ["storage"] = "Azure Key Vault recommended"
                }
            ));
        }

        return patterns;
    }

    
    #endregion
}
