using MemoryAgent.Server.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text.RegularExpressions;

namespace MemoryAgent.Server.CodeAnalysis;

/// <summary>
/// Category 1: Prompt Engineering & Guardrails
/// </summary>
public partial class AIAgentPatternDetector
{
    #region Category 1: Prompt Engineering & Guardrails

    private List<CodePattern> DetectSystemPromptDefinition(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: Large const/static strings with instruction keywords
        var fields = root.DescendantNodes().OfType<FieldDeclarationSyntax>();
        
        foreach (var field in fields)
        {
            if (field.Modifiers.Any(m => m.IsKind(SyntaxKind.ConstKeyword) || m.IsKind(SyntaxKind.StaticKeyword)))
            {
                var fieldText = field.ToString();
                var variable = field.Declaration.Variables.FirstOrDefault();
                var name = variable?.Identifier.Text ?? "";
                
                // Check for prompt/instruction-related names
                if (name.Contains("Prompt") || name.Contains("System") || name.Contains("Persona") ||
                    name.Contains("Instruction") || name.Contains("Policy") || name.Contains("Role"))
                {
                    // Check for instruction keywords in the value
                    if (fieldText.Length > 100 && ContainsInstructionKeywords(fieldText))
                    {
                        var lineNumber = GetLineNumber(root, field, sourceCode);
                        patterns.Add(CreatePattern(
                            name: "AI_SystemPromptDefinition",
                            type: PatternType.AgentLightning,
                            category: PatternCategory.AIAgents,
                            implementation: $"System prompt definition: {name}",
                            filePath: filePath,
                            lineNumber: lineNumber,
                            content: GetContextAroundNode(field, sourceCode, 5),
                            bestPractice: "Define system prompts as constants for reusability and version control. Use Microsoft Guidance or Semantic Kernel for structured prompts.",
                            azureUrl: AzureOpenAIPromptUrl,
                            context: context,
                            confidence: 0.90f,
                            metadata: new Dictionary<string, object>
                            {
                                ["prompt_name"] = name,
                                ["pattern_type"] = "System Prompt",
                                ["best_practices"] = new[] { "Version control", "Prompt templates", "A/B testing" }
                            }
                        ));
                    }
                }
            }
        }

        // Pattern: Classes/records storing prompts
        var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
        
        foreach (var classDecl in classes)
        {
            var className = classDecl.Identifier.Text;
            
            if (className.Contains("Prompt") || className.Contains("Persona") || 
                className.Contains("Policy") || className.Contains("Agent") && className.Contains("Config"))
            {
                var properties = classDecl.DescendantNodes().OfType<PropertyDeclarationSyntax>();
                
                if (properties.Any(p => p.Identifier.Text.Contains("System") || 
                                       p.Identifier.Text.Contains("Message") ||
                                       p.Identifier.Text.Contains("Instruction")))
                {
                    var lineNumber = GetLineNumber(root, classDecl, sourceCode);
                    patterns.Add(CreatePattern(
                        name: "AI_PromptPolicyClass",
                        type: PatternType.AgentLightning,
                        category: PatternCategory.AIAgents,
                        implementation: $"Prompt policy class: {className}",
                        filePath: filePath,
                        lineNumber: lineNumber,
                        content: GetContextAroundNode(classDecl, sourceCode, 10),
                        bestPractice: "Use dedicated classes for agent prompts and policies to enable configuration and testing.",
                        azureUrl: AzureOpenAIPromptUrl,
                        context: context,
                        confidence: 0.88f,
                        metadata: new Dictionary<string, object>
                        {
                            ["class_name"] = className,
                            ["pattern_type"] = "Prompt Class"
                        }
                    ));
                }
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectPromptTemplates(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: Semantic Kernel ChatPromptTemplate
        if (sourceCode.Contains("ChatPromptTemplate") || sourceCode.Contains("PromptTemplate"))
        {
            patterns.Add(CreatePattern(
                name: "AI_PromptTemplate",
                type: PatternType.AgentLightning,
                category: PatternCategory.AIAgents,
                implementation: "Prompt template usage (Semantic Kernel or similar)",
                filePath: filePath,
                lineNumber: 1,
                content: "// Prompt template detected",
                bestPractice: "Use prompt templates for parameterized, reusable prompts. Semantic Kernel and Microsoft Guidance provide robust templating.",
                azureUrl: SemanticKernelUrl,
                context: context,
                confidence: 0.95f,
                metadata: new Dictionary<string, object>
                {
                    ["template_engine"] = "Semantic Kernel or equivalent",
                    ["benefits"] = new[] { "Reusability", "Parameterization", "Version control" }
                }
            ));
        }

        // Pattern: Handlebars-style templates (Microsoft Guidance)
        var handlebarPattern = new Regex(@"\{\{[\w_]+\}\}");
        if (handlebarPattern.IsMatch(sourceCode))
        {
            patterns.Add(CreatePattern(
                name: "AI_HandlebarsTemplate",
                type: PatternType.AgentLightning,
                category: PatternCategory.AIAgents,
                implementation: "Handlebars-style prompt template ({{placeholder}})",
                filePath: filePath,
                lineNumber: 1,
                content: "// {{placeholder}} template syntax detected",
                bestPractice: "Handlebars templates enable structured prompts with placeholders. Consider Microsoft Guidance for advanced constraint-based generation.",
                azureUrl: MicrosoftGuidanceUrl,
                context: context,
                confidence: 0.92f,
                metadata: new Dictionary<string, object>
                {
                    ["template_syntax"] = "Handlebars",
                    ["suggestion"] = "Consider Microsoft Guidance library"
                }
            ));
        }

        // Pattern: String interpolation with multiple variables (basic templating)
        var interpolationPattern = new Regex(@"\$""[^""]*\{[^}]+\}[^""]*\{[^}]+\}");
        if (interpolationPattern.IsMatch(sourceCode))
        {
            patterns.Add(CreatePattern(
                name: "AI_StringInterpolationTemplate",
                type: PatternType.AgentLightning,
                category: PatternCategory.AIAgents,
                implementation: "String interpolation for prompts (basic templating)",
                filePath: filePath,
                lineNumber: 1,
                content: "// String interpolation prompt detected",
                bestPractice: "For complex prompts, consider upgrading to a template engine like Semantic Kernel or Microsoft Guidance for better maintainability.",
                azureUrl: AzureOpenAIPromptUrl,
                context: context,
                confidence: 0.75f,
                metadata: new Dictionary<string, object>
                {
                    ["template_type"] = "String interpolation",
                    ["recommendation"] = "Upgrade to structured template engine"
                }
            ));
        }

        return patterns;
    }

    private List<CodePattern> DetectGuardrailInjection(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: Azure Content Safety usage
        if (sourceCode.Contains("ContentSafetyClient") || sourceCode.Contains("ContentSafety"))
        {
            patterns.Add(CreatePattern(
                name: "AI_ContentSafetyGuardrail",
                type: PatternType.Security,
                category: PatternCategory.Security,
                implementation: "Azure Content Safety integration for content moderation",
                filePath: filePath,
                lineNumber: 1,
                content: "// Azure Content Safety detected",
                bestPractice: "Use Azure Content Safety to moderate harmful content before and after LLM calls.",
                azureUrl: AzureContentSafetyUrl,
                context: context,
                confidence: 0.98f,
                metadata: new Dictionary<string, object>
                {
                    ["service"] = "Azure Content Safety",
                    ["categories"] = new[] { "Hate", "Violence", "Sexual", "Self-harm" }
                }
            ));
        }

        // Pattern: Safety guidelines injection in prompts
        if (sourceCode.Contains("Safety") && (sourceCode.Contains("Guidelines") || sourceCode.Contains("Policy")))
        {
            var safetyPattern = new Regex(@"(Safety|Policy|Guidelines|Prohibited|Must not)", RegexOptions.IgnoreCase);
            if (safetyPattern.IsMatch(sourceCode))
            {
                patterns.Add(CreatePattern(
                    name: "AI_SafetyPolicyInjection",
                    type: PatternType.Security,
                    category: PatternCategory.Security,
                    implementation: "Safety policy/guidelines injection into prompts",
                    filePath: filePath,
                    lineNumber: 1,
                    content: "// Safety policy injection detected",
                    bestPractice: "Inject safety guidelines into system prompts to constrain agent behavior.",
                    azureUrl: AzureOpenAIPromptUrl,
                    context: context,
                    confidence: 0.80f,
                    metadata: new Dictionary<string, object>
                    {
                        ["pattern"] = "Policy Injection",
                        ["purpose"] = "Behavior constraints"
                    }
                ));
            }
        }

        // Pattern: Spotlighting technique (Microsoft's prompt injection mitigation)
        var spotlightPattern = new Regex(@"```(user_input|input|data)");
        if (spotlightPattern.IsMatch(sourceCode))
        {
            patterns.Add(CreatePattern(
                name: "AI_SpotlightingGuardrail",
                type: PatternType.Security,
                category: PatternCategory.Security,
                implementation: "Spotlighting technique for prompt injection mitigation",
                filePath: filePath,
                lineNumber: 1,
                content: "// Spotlighting pattern detected (```user_input```)",
                bestPractice: "Microsoft's Spotlighting technique uses delimiters to separate user input from instructions, mitigating prompt injection attacks.",
                azureUrl: "https://www.microsoft.com/en-us/security/blog/2024/04/11/how-microsoft-discovers-and-mitigates-evolving-attacks-against-ai-guardrails/",
                context: context,
                confidence: 0.92f,
                metadata: new Dictionary<string, object>
                {
                    ["technique"] = "Spotlighting",
                    ["protection"] = "Prompt injection mitigation"
                }
            ));
        }

        return patterns;
    }

    
    #endregion
}
