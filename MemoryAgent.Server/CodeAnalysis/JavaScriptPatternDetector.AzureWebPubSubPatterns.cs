using MemoryAgent.Server.Models;
using System.Text.RegularExpressions;

namespace MemoryAgent.Server.CodeAnalysis;

public partial class JavaScriptPatternDetector
{
    /// <summary>
    /// Detects Azure Web PubSub patterns in JavaScript/TypeScript (client-side)
    /// Covers: WebSocket connections, client SDK, message handling, reconnection logic
    /// </summary>
    private List<CodePattern> DetectAzureWebPubSubPatterns(string sourceCode, string[] lines, string filePath, string? context, string language)
    {
        var patterns = new List<CodePattern>();
        const string WebPubSubUrl = "https://learn.microsoft.com/en-us/azure/azure-web-pubsub/";

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];

            // Pattern 1: WebPubSubClient initialization (client-side SDK)
            if (Regex.IsMatch(line, @"new\s+WebPubSubClient\s*\(|WebPubSubClient\.create\s*\(", RegexOptions.IgnoreCase))
            {
                patterns.Add(CreatePattern(
                    name: "WebPubSubClient_Init",
                    type: PatternType.AzureWebPubSub,
                    category: PatternCategory.RealtimeMessaging,
                    implementation: "WebPubSubClient",
                    language: language,
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 5),
                    bestPractice: "Initializing Azure Web PubSub client for WebSocket connection",
                    azureUrl: WebPubSubUrl,
                    context: context
                ));
            }

            // Pattern 2: WebSocket connection with URL
            if (Regex.IsMatch(line, @"new\s+WebSocket\s*\(.*azure.*webpubsub|wss://.*\.webpubsub\.azure\.com", RegexOptions.IgnoreCase))
            {
                patterns.Add(CreatePattern(
                    name: "WebPubSub_WebSocketConnection",
                    type: PatternType.AzureWebPubSub,
                    category: PatternCategory.RealtimeMessaging,
                    implementation: "WebSocket",
                    language: language,
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 5),
                    bestPractice: "Establishing WebSocket connection to Azure Web PubSub",
                    azureUrl: WebPubSubUrl,
                    context: context
                ));
            }

            // Pattern 3: client.start() - Connection initialization
            if (Regex.IsMatch(line, @"(client|webPubSubClient)\.(start|connect)\s*\(", RegexOptions.IgnoreCase))
            {
                var hasAwait = Regex.IsMatch(line, @"\bawait\b", RegexOptions.IgnoreCase);
                var hasTryCatch = lines.Skip(Math.Max(0, i - 5)).Take(10).Any(l => 
                    Regex.IsMatch(l, @"\btry\b|\bcatch\b", RegexOptions.IgnoreCase));

                patterns.Add(CreatePattern(
                    name: "WebPubSub_StartConnection",
                    type: PatternType.AzureWebPubSub,
                    category: PatternCategory.ConnectionManagement,
                    implementation: "start/connect",
                    language: language,
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 5),
                    bestPractice: $"Starting WebSocket connection{(hasAwait ? " (async)" : " (WARNING: Use await)")}{(hasTryCatch ? "" : " WARNING: Add try/catch")}",
                    azureUrl: WebPubSubUrl,
                    context: context
                ));
            }

            // Pattern 4: client.sendToGroup() - Group messaging
            if (Regex.IsMatch(line, @"\.(sendToGroup|sendEvent)\s*\(", RegexOptions.IgnoreCase))
            {
                var hasAwait = Regex.IsMatch(line, @"\bawait\b", RegexOptions.IgnoreCase);
                
                patterns.Add(CreatePattern(
                    name: "WebPubSub_SendToGroup",
                    type: PatternType.AzureWebPubSub,
                    category: PatternCategory.RealtimeMessaging,
                    implementation: "sendToGroup",
                    language: language,
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 5),
                    bestPractice: $"Sending message to group{(hasAwait ? " (async)" : "")}",
                    azureUrl: WebPubSubUrl,
                    context: context
                ));
            }

            // Pattern 5: client.on('connected') - Event handlers
            // Match both single and double quotes
            var eventMatch = Regex.Match(line, @"\.on\s*\(\s*[""']?(connected|disconnected|group-message|server-message)[""']?\s*,", RegexOptions.IgnoreCase);
            if (eventMatch.Success)
            {
                var eventType = eventMatch.Groups[1].Value;
                
                var pattern = CreatePattern(
                    name: $"WebPubSub_{eventType}Handler",
                    type: PatternType.AzureWebPubSub,
                    category: PatternCategory.EventHandlers,
                    implementation: $"on('{eventType}')",
                    language: language,
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 7),
                    bestPractice: $"Handling Web PubSub {eventType} event",
                    azureUrl: WebPubSubUrl,
                    context: context
                );
                pattern.Metadata["EventType"] = eventType;
                patterns.Add(pattern);
            }
            
            // Also catch general .on() event handlers for client object
            else if (Regex.IsMatch(line, @"client\.on\s*\(", RegexOptions.IgnoreCase))
            {
                // Extract event name from the line
                var generalEventMatch = Regex.Match(line, @"\.on\s*\(\s*[""']([^""']+)[""']", RegexOptions.IgnoreCase);
                if (generalEventMatch.Success)
                {
                    var eventName = generalEventMatch.Groups[1].Value;
                    var pattern = CreatePattern(
                        name: $"WebPubSub_{eventName}Handler",
                        type: PatternType.AzureWebPubSub,
                        category: PatternCategory.EventHandlers,
                        implementation: $"on('{eventName}')",
                        language: language,
                        filePath: filePath,
                        lineNumber: i + 1,
                        content: GetContext(lines, i, 5),
                        bestPractice: $"Handling Web PubSub {eventName} event",
                        azureUrl: WebPubSubUrl,
                        context: context
                    );
                    pattern.Metadata["EventType"] = eventName;
                    patterns.Add(pattern);
                }
            }

            // Pattern 6: Reconnection logic
            if (Regex.IsMatch(line, @"reconnect|retryConnection|autoReconnect\s*[:=]\s*true", RegexOptions.IgnoreCase))
            {
                var hasBackoff = lines.Skip(i).Take(10).Any(l => 
                    Regex.IsMatch(l, @"backoff|delay|exponential|retry", RegexOptions.IgnoreCase));
                
                patterns.Add(CreatePattern(
                    name: "WebPubSub_ReconnectionLogic",
                    type: PatternType.AzureWebPubSub,
                    category: PatternCategory.ConnectionManagement,
                    implementation: "Reconnection",
                    language: language,
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 7),
                    bestPractice: hasBackoff 
                        ? "Reconnection logic with backoff strategy" 
                        : "Reconnection logic (Consider adding exponential backoff)",
                    azureUrl: WebPubSubUrl,
                    context: context
                ));
            }

            // Pattern 7: Join group
            if (Regex.IsMatch(line, @"\.(joinGroup|subscribe)\s*\(", RegexOptions.IgnoreCase))
            {
                var hasAwait = Regex.IsMatch(line, @"\bawait\b", RegexOptions.IgnoreCase);
                
                patterns.Add(CreatePattern(
                    name: "WebPubSub_JoinGroup",
                    type: PatternType.AzureWebPubSub,
                    category: PatternCategory.ConnectionManagement,
                    implementation: "joinGroup",
                    language: language,
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 5),
                    bestPractice: $"Joining group for targeted messaging{(hasAwait ? " (async)" : "")}",
                    azureUrl: WebPubSubUrl,
                    context: context
                ));
            }

            // Pattern 8: Leave group
            if (Regex.IsMatch(line, @"\.(leaveGroup|unsubscribe)\s*\(", RegexOptions.IgnoreCase))
            {
                var hasAwait = Regex.IsMatch(line, @"\bawait\b", RegexOptions.IgnoreCase);
                
                patterns.Add(CreatePattern(
                    name: "WebPubSub_LeaveGroup",
                    type: PatternType.AzureWebPubSub,
                    category: PatternCategory.ConnectionManagement,
                    implementation: "leaveGroup",
                    language: language,
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 5),
                    bestPractice: $"Leaving group{(hasAwait ? " (async)" : "")}",
                    azureUrl: WebPubSubUrl,
                    context: context
                ));
            }

            // Pattern 9: Connection state management
            if (Regex.IsMatch(line, @"connectionState|readyState|isConnected", RegexOptions.IgnoreCase) &&
                Regex.IsMatch(line, @"webpubsub|websocket", RegexOptions.IgnoreCase))
            {
                patterns.Add(CreatePattern(
                    name: "WebPubSub_ConnectionState",
                    type: PatternType.AzureWebPubSub,
                    category: PatternCategory.ConnectionManagement,
                    implementation: "Connection State",
                    language: language,
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 5),
                    bestPractice: "Tracking WebSocket connection state",
                    azureUrl: WebPubSubUrl,
                    context: context
                ));
            }

            // Pattern 10: Error handling
            if (Regex.IsMatch(line, @"\.(on|addEventListener)\s*\(\s*[""'](error|close)[""']", RegexOptions.IgnoreCase) &&
                lines.Skip(Math.Max(0, i - 5)).Take(10).Any(l => Regex.IsMatch(l, @"webpubsub|websocket", RegexOptions.IgnoreCase)))
            {
                var hasLogging = lines.Skip(i).Take(10).Any(l => 
                    Regex.IsMatch(l, @"console\.(log|error|warn)|logger\.", RegexOptions.IgnoreCase));
                
                patterns.Add(CreatePattern(
                    name: "WebPubSub_ErrorHandler",
                    type: PatternType.AzureWebPubSub,
                    category: PatternCategory.EventHandlers,
                    implementation: "Error Handler",
                    language: language,
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 7),
                    bestPractice: hasLogging 
                        ? "Error handler with logging" 
                        : "Error handler (Consider adding logging)",
                    azureUrl: WebPubSubUrl,
                    context: context
                ));
            }

            // Pattern 11: React hook for Web PubSub
            if (Regex.IsMatch(line, @"useWebPubSub|useWebSocket.*webpubsub", RegexOptions.IgnoreCase))
            {
                patterns.Add(CreatePattern(
                    name: "WebPubSub_ReactHook",
                    type: PatternType.AzureWebPubSub,
                    category: PatternCategory.RealtimeMessaging,
                    implementation: "React Hook",
                    language: "typescript",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 7),
                    bestPractice: "Using React hook for Web PubSub integration",
                    azureUrl: WebPubSubUrl,
                    context: context
                ));
            }

            // Pattern 12: Message size validation (1MB limit)
            if (Regex.IsMatch(line, @"\.length\s*[<>]=?\s*\d+.*1024.*1024|\.size.*MB|message.*size", RegexOptions.IgnoreCase) &&
                lines.Skip(Math.Max(0, i - 5)).Take(10).Any(l => Regex.IsMatch(l, @"send|publish", RegexOptions.IgnoreCase)))
            {
                patterns.Add(CreatePattern(
                    name: "WebPubSub_MessageSizeValidation",
                    type: PatternType.AzureWebPubSub,
                    category: PatternCategory.Reliability,
                    implementation: "Message Size Check",
                    language: language,
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 5),
                    bestPractice: "Validating message size before sending (Azure Web PubSub has 1MB limit)",
                    azureUrl: WebPubSubUrl,
                    context: context
                ));
            }
        }

        return patterns;
    }
}

