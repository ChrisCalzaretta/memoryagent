using MemoryAgent.Server.Models;
using System.Text.RegularExpressions;

namespace MemoryAgent.Server.CodeAnalysis;

public partial class VBNetPatternDetector
{
    /// <summary>
    /// Detects Azure Web PubSub patterns in VB.NET
    /// Covers: Service Client, Connection Management, Messaging, Authentication, Event Handlers
    /// </summary>
    private List<CodePattern> DetectAzureWebPubSubPatterns(string[] lines, string filePath, string? context)
    {
        var patterns = new List<CodePattern>();
        const string WebPubSubUrl = "https://learn.microsoft.com/en-us/azure/azure-web-pubsub/";

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];

            // Pattern 1: WebPubSubServiceClient initialization
            if (Regex.IsMatch(line, @"(New\s+WebPubSubServiceClient|WebPubSubServiceClient)\s*\(", RegexOptions.IgnoreCase))
            {
                var usesConfig = Regex.IsMatch(line, @"Configuration|ConfigurationManager|GetConnectionString", RegexOptions.IgnoreCase);
                
                patterns.Add(CreatePattern(
                    name: "WebPubSubServiceClient_Init",
                    type: PatternType.AzureWebPubSub,
                    category: PatternCategory.RealtimeMessaging,
                    implementation: "WebPubSubServiceClient",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 5),
                    bestPractice: usesConfig
                        ? "Service client initialized with connection string from configuration"
                        : "Service client initialized (WARNING: Use configuration, not hardcoded strings)",
                    azureUrl: WebPubSubUrl,
                    context: context
                ));
            }

            // Pattern 2: SendToAllAsync - Broadcast messaging
            if (Regex.IsMatch(line, @"(\.SendToAllAsync|client\.SendToAllAsync|SendToAllAsync)\s*\(", RegexOptions.IgnoreCase))
            {
                var hasAwait = Regex.IsMatch(line, @"\bAwait\b", RegexOptions.IgnoreCase);
                var hasTryCatch = lines.Skip(Math.Max(0, i - 5)).Take(10).Any(l => 
                    Regex.IsMatch(l, @"\bTry\b", RegexOptions.IgnoreCase));

                patterns.Add(CreatePattern(
                    name: "WebPubSub_BroadcastMessage",
                    type: PatternType.AzureWebPubSub,
                    category: PatternCategory.RealtimeMessaging,
                    implementation: "SendToAllAsync",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 5),
                    bestPractice: $"Broadcasting to all clients{(hasAwait ? " (async)" : " (WARNING: Use Await)")}{(hasTryCatch ? "" : " WARNING: Add Try/Catch")}",
                    azureUrl: WebPubSubUrl,
                    context: context
                ));
            }

            // Pattern 3: SendToGroupAsync - Group messaging
            if (Regex.IsMatch(line, @"\.SendToGroupAsync\s*\(", RegexOptions.IgnoreCase))
            {
                var hasAwait = Regex.IsMatch(line, @"\bAwait\b", RegexOptions.IgnoreCase);
                
                patterns.Add(CreatePattern(
                    name: "WebPubSub_GroupMessage",
                    type: PatternType.AzureWebPubSub,
                    category: PatternCategory.RealtimeMessaging,
                    implementation: "SendToGroupAsync",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 5),
                    bestPractice: $"Sending to specific group{(hasAwait ? " (async)" : " (WARNING: Use Await)")}",
                    azureUrl: WebPubSubUrl,
                    context: context
                ));
            }

            // Pattern 4: SendToUserAsync - User messaging
            if (Regex.IsMatch(line, @"\.SendToUserAsync\s*\(", RegexOptions.IgnoreCase))
            {
                var hasAwait = Regex.IsMatch(line, @"\bAwait\b", RegexOptions.IgnoreCase);
                
                patterns.Add(CreatePattern(
                    name: "WebPubSub_UserMessage",
                    type: PatternType.AzureWebPubSub,
                    category: PatternCategory.RealtimeMessaging,
                    implementation: "SendToUserAsync",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 5),
                    bestPractice: $"Sending to specific user{(hasAwait ? " (async)" : " (WARNING: Use Await)")}",
                    azureUrl: WebPubSubUrl,
                    context: context
                ));
            }

            // Pattern 5: GetClientAccessUri - Token generation
            if (Regex.IsMatch(line, @"\.GetClientAccessUri\s*\(", RegexOptions.IgnoreCase))
            {
                var hasUserId = Regex.IsMatch(line, @"userId\s*:=", RegexOptions.IgnoreCase);
                var hasRoles = Regex.IsMatch(line, @"roles\s*:=", RegexOptions.IgnoreCase);
                
                patterns.Add(CreatePattern(
                    name: "WebPubSub_ClientAccessToken",
                    type: PatternType.AzureWebPubSub,
                    category: PatternCategory.Security,
                    implementation: "GetClientAccessUri",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 5),
                    bestPractice: $"Generating client access token{(hasUserId ? " with user ID" : "")}{(hasRoles ? " and roles" : "")}",
                    azureUrl: WebPubSubUrl,
                    context: context
                ));
            }

            // Pattern 6: AddConnectionToGroupAsync - Group management
            if (Regex.IsMatch(line, @"\.AddConnectionToGroupAsync\s*\(", RegexOptions.IgnoreCase))
            {
                patterns.Add(CreatePattern(
                    name: "WebPubSub_AddToGroup",
                    type: PatternType.AzureWebPubSub,
                    category: PatternCategory.ConnectionManagement,
                    implementation: "AddConnectionToGroupAsync",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 5),
                    bestPractice: "Adding connection to group for targeted messaging",
                    azureUrl: WebPubSubUrl,
                    context: context
                ));
            }

            // Pattern 7: RemoveConnectionFromGroupAsync
            if (Regex.IsMatch(line, @"\.RemoveConnectionFromGroupAsync\s*\(", RegexOptions.IgnoreCase))
            {
                patterns.Add(CreatePattern(
                    name: "WebPubSub_RemoveFromGroup",
                    type: PatternType.AzureWebPubSub,
                    category: PatternCategory.ConnectionManagement,
                    implementation: "RemoveConnectionFromGroupAsync",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 5),
                    bestPractice: "Removing connection from group",
                    azureUrl: WebPubSubUrl,
                    context: context
                ));
            }

            // Pattern 8: CloseConnectionAsync
            if (Regex.IsMatch(line, @"\.CloseConnectionAsync\s*\(|\.CloseClientConnectionAsync\s*\(", RegexOptions.IgnoreCase))
            {
                patterns.Add(CreatePattern(
                    name: "WebPubSub_CloseConnection",
                    type: PatternType.AzureWebPubSub,
                    category: PatternCategory.ConnectionManagement,
                    implementation: "CloseConnectionAsync",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 5),
                    bestPractice: "Gracefully closing client connection",
                    azureUrl: WebPubSubUrl,
                    context: context
                ));
            }

            // Pattern 9: DefaultAzureCredential / ManagedIdentityCredential
            if (Regex.IsMatch(line, @"New\s+(DefaultAzureCredential|ManagedIdentityCredential)\s*\(", RegexOptions.IgnoreCase))
            {
                var usesManagedIdentity = line.Contains("ManagedIdentityCredential");
                
                patterns.Add(CreatePattern(
                    name: "WebPubSub_Authentication",
                    type: PatternType.AzureWebPubSub,
                    category: PatternCategory.Security,
                    implementation: usesManagedIdentity ? "ManagedIdentityCredential" : "DefaultAzureCredential",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 5),
                    bestPractice: $"Using Azure AD authentication{(usesManagedIdentity ? " with Managed Identity (recommended)" : "")}",
                    azureUrl: WebPubSubUrl,
                    context: context
                ));
            }

            // Pattern 10: Event Handler attributes
            if (Regex.IsMatch(line, @"<HttpPost.*>|Function.*Event.*HttpPost", RegexOptions.IgnoreCase) &&
                lines.Skip(i).Take(10).Any(l => Regex.IsMatch(l, @"WebPubSub|EventRequest", RegexOptions.IgnoreCase)))
            {
                var hasValidation = lines.Skip(i).Take(15).Any(l => 
                    Regex.IsMatch(l, @"Validate|VerifySignature", RegexOptions.IgnoreCase));
                
                patterns.Add(CreatePattern(
                    name: "WebPubSub_WebhookEndpoint",
                    type: PatternType.AzureWebPubSub,
                    category: PatternCategory.EventHandlers,
                    implementation: "HTTP Webhook",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 10),
                    bestPractice: hasValidation 
                        ? "Webhook endpoint with signature validation" 
                        : "Webhook endpoint (CRITICAL: Must validate signatures!)",
                    azureUrl: WebPubSubUrl,
                    context: context
                ));
            }
        }

        return patterns;
    }
}

