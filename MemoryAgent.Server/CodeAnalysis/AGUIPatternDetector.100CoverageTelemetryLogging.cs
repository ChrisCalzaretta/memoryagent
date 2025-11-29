using MemoryAgent.Server.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text.RegularExpressions;

namespace MemoryAgent.Server.CodeAnalysis;

/// <summary>
/// 100% Coverage - Telemetry & Logging
/// </summary>
public partial class AGUIPatternDetector
{
    #region 100% Coverage - Telemetry & Logging

    private List<CodePattern> DetectTelemetryLogging(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: OpenTelemetry
        if (sourceCode.Contains("OpenTelemetry") || sourceCode.Contains("ActivitySource") ||
            sourceCode.Contains("Tracer") || sourceCode.Contains("Meter"))
        {
            patterns.Add(CreatePattern(
                name: "AGUI_OpenTelemetry",
                type: PatternType.AGUI,
                category: PatternCategory.Operational,
                implementation: "OpenTelemetry instrumentation for AG-UI",
                filePath: filePath,
                lineNumber: 1,
                content: "// OpenTelemetry detected",
                bestPractice: "Use OpenTelemetry to trace AG-UI agent runs, tool calls, and streaming operations for observability.",
                azureUrl: "https://learn.microsoft.com/en-us/azure/azure-monitor/app/opentelemetry-enable",
                context: context,
                confidence: 0.95f,
                metadata: new Dictionary<string, object>
                {
                    ["telemetry"] = "OpenTelemetry",
                    ["capabilities"] = new[] { "Tracing", "Metrics", "Logging" }
                }
            ));
        }

        // Pattern: Structured logging
        if ((sourceCode.Contains("ILogger") || sourceCode.Contains("Serilog")) &&
            (sourceCode.Contains("agent") || sourceCode.Contains("AG-UI") || sourceCode.Contains("tool")))
        {
            patterns.Add(CreatePattern(
                name: "AGUI_StructuredLogging",
                type: PatternType.AGUI,
                category: PatternCategory.Operational,
                implementation: "Structured logging for AG-UI events",
                filePath: filePath,
                lineNumber: 1,
                content: "// Structured logging detected",
                bestPractice: "Use structured logging to capture AG-UI events with proper context (thread IDs, tool names, user IDs).",
                azureUrl: "https://learn.microsoft.com/en-us/dotnet/core/extensions/logging",
                context: context,
                confidence: 0.82f,
                metadata: new Dictionary<string, object>
                {
                    ["pattern"] = "Structured Logging",
                    ["context_fields"] = new[] { "ThreadID", "ToolName", "UserID", "EventType" }
                }
            ));
        }

        // Pattern: Application Insights
        if (sourceCode.Contains("TelemetryClient") || sourceCode.Contains("ApplicationInsights"))
        {
            patterns.Add(CreatePattern(
                name: "AGUI_ApplicationInsights",
                type: PatternType.AGUI,
                category: PatternCategory.Operational,
                implementation: "Azure Application Insights for AG-UI monitoring",
                filePath: filePath,
                lineNumber: 1,
                content: "// Application Insights detected",
                bestPractice: "Use Application Insights to monitor AG-UI performance, errors, and usage patterns.",
                azureUrl: "https://learn.microsoft.com/en-us/azure/azure-monitor/app/app-insights-overview",
                context: context,
                confidence: 0.93f,
                metadata: new Dictionary<string, object>
                {
                    ["service"] = "Application Insights",
                    ["metrics"] = new[] { "Response time", "Error rate", "Active connections" }
                }
            ));
        }

        return patterns;
    }

    
    #endregion
}
