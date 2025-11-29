using MemoryAgent.Server.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text.RegularExpressions;

namespace MemoryAgent.Server.CodeAnalysis;

/// <summary>
/// Category 3: Tools & Function Calling
/// </summary>
public partial class AIAgentPatternDetector
{
    #region Category 3: Tools & Function Calling

    private List<CodePattern> DetectToolRegistration(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: Semantic Kernel [KernelFunction] attribute
        var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();
        
        foreach (var method in methods)
        {
            var attributes = method.AttributeLists.SelectMany(al => al.Attributes);
            
            if (attributes.Any(a => a.Name.ToString().Contains("KernelFunction")))
            {
                var methodName = method.Identifier.Text;
                var lineNumber = GetLineNumber(root, method, sourceCode);
                
                patterns.Add(CreatePattern(
                    name: "AI_KernelFunctionRegistration",
                    type: PatternType.AgentLightning,
                    category: PatternCategory.ToolIntegration,
                    implementation: $"Kernel function: {methodName}",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(method, sourceCode, 8),
                    bestPractice: "Use [KernelFunction] attribute to register functions/tools that agents can call. Add [Description] for better LLM understanding.",
                    azureUrl: SemanticKernelUrl,
                    context: context,
                    confidence: 0.98f,
                    metadata: new Dictionary<string, object>
                    {
                        ["function_name"] = methodName,
                        ["framework"] = "Semantic Kernel",
                        ["pattern_significance"] = "HIGH - Agent tool capability"
                    }
                ));
            }
        }

        // Pattern: Tool/function collections
        if (sourceCode.Contains("FunctionDef") || sourceCode.Contains("ToolManifest") ||
            sourceCode.Contains("ToolRegistry") || sourceCode.Contains("List<Tool>"))
        {
            patterns.Add(CreatePattern(
                name: "AI_ToolCollection",
                type: PatternType.AgentLightning,
                category: PatternCategory.ToolIntegration,
                implementation: "Tool/function collection for agent capabilities",
                filePath: filePath,
                lineNumber: 1,
                content: "// Tool collection detected",
                bestPractice: "Maintain a registry of available tools/functions for the agent. Enables dynamic tool discovery and execution.",
                azureUrl: SemanticKernelUrl,
                context: context,
                confidence: 0.90f,
                metadata: new Dictionary<string, object>
                {
                    ["pattern"] = "Tool Collection",
                    ["significance"] = "Distinguishes agent from simple LLM call"
                }
            ));
        }

        // Pattern: ITool interface implementations
        var interfaces = root.DescendantNodes().OfType<InterfaceDeclarationSyntax>();
        
        foreach (var interfaceDecl in interfaces)
        {
            var interfaceName = interfaceDecl.Identifier.Text;
            
            if (interfaceName == "ITool" || interfaceName == "IPlugin" || interfaceName == "IFunction")
            {
                var lineNumber = GetLineNumber(root, interfaceDecl, sourceCode);
                patterns.Add(CreatePattern(
                    name: "AI_ToolInterface",
                    type: PatternType.AgentLightning,
                    category: PatternCategory.ToolIntegration,
                    implementation: $"Tool interface definition: {interfaceName}",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(interfaceDecl, sourceCode, 10),
                    bestPractice: "Define standard tool interfaces for consistent agent tool integration.",
                    azureUrl: SemanticKernelUrl,
                    context: context,
                    confidence: 0.92f,
                    metadata: new Dictionary<string, object>
                    {
                        ["interface_name"] = interfaceName
                    }
                ));
                break;
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectToolRouting(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: FunctionCall inspection and dispatch
        if ((sourceCode.Contains("FunctionCall") || sourceCode.Contains("ToolCall")) &&
            (sourceCode.Contains("Execute") || sourceCode.Contains("Invoke") || sourceCode.Contains("Dispatch")))
        {
            patterns.Add(CreatePattern(
                name: "AI_ToolRouting",
                type: PatternType.AgentLightning,
                category: PatternCategory.ToolIntegration,
                implementation: "Tool routing/dispatch based on LLM function calls",
                filePath: filePath,
                lineNumber: 1,
                content: "// Tool routing detected",
                bestPractice: "Inspect LLM outputs for function/tool calls and route to appropriate handlers. Core pattern for agentic behavior.",
                azureUrl: AzureOpenAIPromptUrl,
                context: context,
                confidence: 0.88f,
                metadata: new Dictionary<string, object>
                {
                    ["pattern"] = "Tool Routing",
                    ["significance"] = "CRITICAL - Enables agent actions beyond text generation"
                }
            ));
        }

        // Pattern: ReAct-style action dispatch in loops
        if (sourceCode.Contains("Action") && sourceCode.Contains("while"))
        {
            var actionDispatchPattern = new Regex(@"while.*\{[^}]*(Action|Tool|Function).*Execute", RegexOptions.Singleline);
            if (actionDispatchPattern.IsMatch(sourceCode))
            {
                patterns.Add(CreatePattern(
                    name: "AI_ActionDispatchLoop",
                    type: PatternType.AgentLightning,
                    category: PatternCategory.ToolIntegration,
                    implementation: "Action dispatch in agent loop (ReAct pattern)",
                    filePath: filePath,
                    lineNumber: 1,
                    content: "// Action dispatch loop detected",
                    bestPractice: "ReAct pattern: loop of reasoning → action → observation. Enables autonomous multi-step agent behavior.",
                    azureUrl: "https://arxiv.org/abs/2210.03629",
                    context: context,
                    confidence: 0.85f,
                    metadata: new Dictionary<string, object>
                    {
                        ["pattern"] = "ReAct Tool Dispatch",
                        ["significance"] = "HIGH - Autonomous agent loop"
                    }
                ));
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectExternalServiceTool(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: Tools with HttpClient (external APIs)
        var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
        
        foreach (var classDecl in classes)
        {
            var className = classDecl.Identifier.Text;
            
            if (className.Contains("Tool") || className.Contains("Plugin") || className.Contains("Function"))
            {
                var classText = classDecl.ToString();
                
                if (classText.Contains("HttpClient") || classText.Contains("_http") || classText.Contains("_client"))
                {
                    var lineNumber = GetLineNumber(root, classDecl, sourceCode);
                    patterns.Add(CreatePattern(
                        name: "AI_ExternalAPITool",
                        type: PatternType.AgentLightning,
                        category: PatternCategory.ToolIntegration,
                        implementation: $"External API tool: {className}",
                        filePath: filePath,
                        lineNumber: lineNumber,
                        content: GetContextAroundNode(classDecl, sourceCode, 10),
                        bestPractice: "Tools that call external APIs extend agent capabilities beyond the model. Ensure proper error handling and timeouts.",
                        azureUrl: SemanticKernelUrl,
                        context: context,
                        confidence: 0.90f,
                        metadata: new Dictionary<string, object>
                        {
                            ["tool_class"] = className,
                            ["integration_type"] = "External API",
                            ["significance"] = "Agent has external capabilities"
                        }
                    ));
                }
            }
        }

        // Pattern: Database access tools
        if ((sourceCode.Contains("SqlConnection") || sourceCode.Contains("DbContext")) &&
            (sourceCode.Contains("Tool") || sourceCode.Contains("Function")))
        {
            patterns.Add(CreatePattern(
                name: "AI_DatabaseTool",
                type: PatternType.AgentLightning,
                category: PatternCategory.ToolIntegration,
                implementation: "Database access tool for agent",
                filePath: filePath,
                lineNumber: 1,
                content: "// Database tool detected",
                bestPractice: "Database tools enable agents to query and manipulate data. Implement proper authorization and SQL injection protection.",
                azureUrl: SemanticKernelUrl,
                context: context,
                confidence: 0.88f,
                metadata: new Dictionary<string, object>
                {
                    ["integration_type"] = "Database",
                    ["security_note"] = "Requires SQL injection protection"
                }
            ));
        }

        // Pattern: File system tools
        if ((sourceCode.Contains("File.Read") || sourceCode.Contains("Directory.") || sourceCode.Contains("FileSystem")) &&
            (sourceCode.Contains("Tool") || sourceCode.Contains("Function")))
        {
            patterns.Add(CreatePattern(
                name: "AI_FileSystemTool",
                type: PatternType.AgentLightning,
                category: PatternCategory.ToolIntegration,
                implementation: "File system access tool",
                filePath: filePath,
                lineNumber: 1,
                content: "// File system tool detected",
                bestPractice: "File system tools enable agents to read/write files. Implement path validation and access controls.",
                azureUrl: SemanticKernelUrl,
                context: context,
                confidence: 0.85f,
                metadata: new Dictionary<string, object>
                {
                    ["integration_type"] = "File System",
                    ["security_note"] = "Requires path validation"
                }
            ));
        }

        return patterns;
    }

    
    #endregion
}
