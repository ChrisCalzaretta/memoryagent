using MemoryAgent.Server.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text.RegularExpressions;

namespace MemoryAgent.Server.CodeAnalysis;

/// <summary>
/// Detects Azure Web PubSub service patterns - Real-time WebSocket messaging and pub/sub patterns
/// 
/// Based on Azure Web PubSub documentation:
/// https://learn.microsoft.com/en-us/azure/azure-web-pubsub/
/// 
/// Detects patterns for:
/// - Service client initialization and configuration
/// - Connection management and lifecycle
/// - Messaging patterns (broadcast, group, user-specific)
/// - Authentication and authorization
/// - Event handler patterns (webhooks)
/// - Resilience and error handling
/// </summary>
public class AzureWebPubSubPatternDetector
{
    private readonly ILogger<AzureWebPubSubPatternDetector>? _logger;

    // Microsoft documentation URLs
    private const string AzureWebPubSubDocsUrl = "https://learn.microsoft.com/en-us/azure/azure-web-pubsub/";
    private const string AzureWebPubSubOverviewUrl = "https://learn.microsoft.com/en-us/azure/azure-web-pubsub/overview";
    private const string AzureWebPubSubKeyConceptsUrl = "https://learn.microsoft.com/en-us/azure/azure-web-pubsub/key-concepts";
    private const string AzureWebPubSubAuthUrl = "https://learn.microsoft.com/en-us/azure/azure-web-pubsub/howto-authorize-from-application";
    private const string AzureWebPubSubEventHandlerUrl = "https://learn.microsoft.com/en-us/azure/azure-web-pubsub/concept-service-internals";
    private const string AzureWebPubSubBestPracticesUrl = "https://learn.microsoft.com/en-us/azure/azure-web-pubsub/concept-performance";

    public AzureWebPubSubPatternDetector(ILogger<AzureWebPubSubPatternDetector>? logger = null)
    {
        _logger = logger;
    }

    public async Task<List<CodePattern>> DetectPatternsAsync(
        string filePath,
        string? context,
        string sourceCode,
        CancellationToken cancellationToken = default)
    {
        var patterns = new List<CodePattern>();

        try
        {
            var tree = CSharpSyntaxTree.ParseText(sourceCode, cancellationToken: cancellationToken);
            var root = await tree.GetRootAsync(cancellationToken);

            // Detect Azure Web PubSub patterns
            patterns.AddRange(DetectServiceClientInitialization(root, filePath, context, sourceCode));
            patterns.AddRange(DetectConnectionManagement(root, filePath, context, sourceCode));
            patterns.AddRange(DetectBroadcastMessaging(root, filePath, context, sourceCode));
            patterns.AddRange(DetectGroupMessaging(root, filePath, context, sourceCode));
            patterns.AddRange(DetectUserMessaging(root, filePath, context, sourceCode));
            patterns.AddRange(DetectAuthentication(root, filePath, context, sourceCode));
            patterns.AddRange(DetectEventHandlers(root, filePath, context, sourceCode));
            patterns.AddRange(DetectHubManagement(root, filePath, context, sourceCode));
            patterns.AddRange(DetectConnectionTokenGeneration(root, filePath, context, sourceCode));
            patterns.AddRange(DetectConnectionLifecycle(root, filePath, context, sourceCode));

            _logger?.LogInformation(
                "Detected {Count} Azure Web PubSub patterns in {FilePath}",
                patterns.Count,
                filePath);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error detecting Azure Web PubSub patterns in {FilePath}", filePath);
        }

        return patterns;
    }

    /// <summary>
    /// Detects WebPubSubServiceClient initialization
    /// </summary>
    private List<CodePattern> DetectServiceClientInitialization(
        SyntaxNode root,
        string filePath,
        string? context,
        string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Detect: new WebPubSubServiceClient(connectionString, hubName)
        var objectCreations = root.DescendantNodes()
            .OfType<ObjectCreationExpressionSyntax>()
            .Where(node => node.Type.ToString().Contains("WebPubSubServiceClient"));

        foreach (var creation in objectCreations)
        {
            var lineNumber = sourceCode.Take(creation.SpanStart).Count(c => c == '\n') + 1;
            var snippet = GetCodeSnippet(sourceCode, lineNumber, 5);

            // Check for connection string in configuration (best practice)
            var usesConfiguration = creation.DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .Any(inv => inv.ToString().Contains("GetConnectionString") || 
                           inv.ToString().Contains("Configuration["));

            patterns.Add(new CodePattern
            {
                Name = "WebPubSubServiceClient_Initialization",
                Type = PatternType.AzureWebPubSub,
                Category = PatternCategory.RealtimeMessaging,
                Implementation = "WebPubSubServiceClient",
                Language = "C#",
                FilePath = filePath,
                LineNumber = lineNumber,
                EndLineNumber = lineNumber + creation.ToString().Split('\n').Length - 1,
                Content = snippet,
                BestPractice = usesConfiguration
                    ? "Service client initialized with connection string from configuration (best practice)"
                    : "Service client initialized (WARNING: Ensure connection string is from configuration, not hardcoded)",
                AzureBestPracticeUrl = AzureWebPubSubOverviewUrl,
                Confidence = usesConfiguration ? 0.95f : 0.75f,
                Context = context ?? string.Empty,
                Metadata = new Dictionary<string, object>
                {
                    ["UsesConfiguration"] = usesConfiguration,
                    ["PatternSubType"] = "ServiceClientInitialization"
                }
            });
        }

        return patterns;
    }

    /// <summary>
    /// Detects connection management patterns
    /// </summary>
    private List<CodePattern> DetectConnectionManagement(
        SyntaxNode root,
        string filePath,
        string? context,
        string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Detect: serviceClient.GetClientAccessUri(), SendToAllAsync, etc.
        var invocations = root.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(inv =>
            {
                var invText = inv.ToString();
                return invText.Contains("GetClientAccessUri") ||
                       invText.Contains("RemoveConnectionFromGroupAsync") ||
                       invText.Contains("AddConnectionToGroupAsync") ||
                       invText.Contains("ConnectionExists") ||
                       invText.Contains("CloseClientConnectionAsync");
            });

        foreach (var invocation in invocations)
        {
            var lineNumber = sourceCode.Take(invocation.SpanStart).Count(c => c == '\n') + 1;
            var snippet = GetCodeSnippet(sourceCode, lineNumber, 5);
            var invText = invocation.ToString();

            string patternName = "Unknown";
            string bestPractice = "";
            string patternSubType = "";

            if (invText.Contains("GetClientAccessUri"))
            {
                patternName = "WebPubSub_ClientAccessUri";
                bestPractice = "Generating client access URI for WebSocket connection with proper authentication";
                patternSubType = "ConnectionTokenGeneration";
            }
            else if (invText.Contains("AddConnectionToGroupAsync"))
            {
                patternName = "WebPubSub_AddToGroup";
                bestPractice = "Adding connection to group for targeted messaging";
                patternSubType = "GroupManagement";
            }
            else if (invText.Contains("RemoveConnectionFromGroupAsync"))
            {
                patternName = "WebPubSub_RemoveFromGroup";
                bestPractice = "Removing connection from group";
                patternSubType = "GroupManagement";
            }
            else if (invText.Contains("CloseClientConnectionAsync"))
            {
                patternName = "WebPubSub_CloseConnection";
                bestPractice = "Gracefully closing client connection";
                patternSubType = "ConnectionLifecycle";
            }

            patterns.Add(new CodePattern
            {
                Name = patternName,
                Type = PatternType.AzureWebPubSub,
                Category = PatternCategory.ConnectionManagement,
                Implementation = "WebPubSubServiceClient",
                Language = "C#",
                FilePath = filePath,
                LineNumber = lineNumber,
                EndLineNumber = lineNumber + invocation.ToString().Split('\n').Length - 1,
                Content = snippet,
                BestPractice = bestPractice,
                AzureBestPracticeUrl = AzureWebPubSubKeyConceptsUrl,
                Confidence = 0.90f,
                Context = context ?? string.Empty,
                Metadata = new Dictionary<string, object>
                {
                    ["PatternSubType"] = patternSubType
                }
            });
        }

        return patterns;
    }

    /// <summary>
    /// Detects broadcast messaging patterns (send to all)
    /// </summary>
    private List<CodePattern> DetectBroadcastMessaging(
        SyntaxNode root,
        string filePath,
        string? context,
        string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Detect: SendToAllAsync, SendToAll
        var invocations = root.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(inv => inv.ToString().Contains("SendToAllAsync") || 
                         inv.ToString().Contains("SendToAll("));

        foreach (var invocation in invocations)
        {
            var lineNumber = sourceCode.Take(invocation.SpanStart).Count(c => c == '\n') + 1;
            var snippet = GetCodeSnippet(sourceCode, lineNumber, 5);

            // Check if using async pattern
            var isAsync = invocation.ToString().Contains("Async");

            // Check for proper error handling
            var hasErrorHandling = IsWithinTryCatch(invocation);

            patterns.Add(new CodePattern
            {
                Name = "WebPubSub_BroadcastMessage",
                Type = PatternType.AzureWebPubSub,
                Category = PatternCategory.RealtimeMessaging,
                Implementation = isAsync ? "SendToAllAsync" : "SendToAll",
                Language = "C#",
                FilePath = filePath,
                LineNumber = lineNumber,
                EndLineNumber = lineNumber + invocation.ToString().Split('\n').Length - 1,
                Content = snippet,
                BestPractice = $"Broadcasting message to all connected clients{(isAsync ? " (async)" : "")}. {(hasErrorHandling ? "Has error handling." : "WARNING: Should use try-catch for error handling.")}",
                AzureBestPracticeUrl = AzureWebPubSubKeyConceptsUrl,
                Confidence = hasErrorHandling && isAsync ? 0.95f : 0.80f,
                Context = context ?? string.Empty,
                Metadata = new Dictionary<string, object>
                {
                    ["IsAsync"] = isAsync,
                    ["HasErrorHandling"] = hasErrorHandling,
                    ["PatternSubType"] = "BroadcastMessaging"
                }
            });
        }

        return patterns;
    }

    /// <summary>
    /// Detects group messaging patterns
    /// </summary>
    private List<CodePattern> DetectGroupMessaging(
        SyntaxNode root,
        string filePath,
        string? context,
        string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Detect: SendToGroupAsync, SendToGroup
        var invocations = root.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(inv => inv.ToString().Contains("SendToGroupAsync") || 
                         inv.ToString().Contains("SendToGroup("));

        foreach (var invocation in invocations)
        {
            var lineNumber = sourceCode.Take(invocation.SpanStart).Count(c => c == '\n') + 1;
            var snippet = GetCodeSnippet(sourceCode, lineNumber, 5);

            var isAsync = invocation.ToString().Contains("Async");
            var hasErrorHandling = IsWithinTryCatch(invocation);

            patterns.Add(new CodePattern
            {
                Name = "WebPubSub_GroupMessage",
                Type = PatternType.AzureWebPubSub,
                Category = PatternCategory.RealtimeMessaging,
                Implementation = isAsync ? "SendToGroupAsync" : "SendToGroup",
                Language = "C#",
                FilePath = filePath,
                LineNumber = lineNumber,
                EndLineNumber = lineNumber + invocation.ToString().Split('\n').Length - 1,
                Content = snippet,
                BestPractice = $"Sending message to specific group of connections{(isAsync ? " (async)" : "")}. Enables targeted real-time updates. {(hasErrorHandling ? "Has error handling." : "WARNING: Should use try-catch.")}",
                AzureBestPracticeUrl = AzureWebPubSubKeyConceptsUrl,
                Confidence = hasErrorHandling && isAsync ? 0.95f : 0.80f,
                Context = context ?? string.Empty,
                Metadata = new Dictionary<string, object>
                {
                    ["IsAsync"] = isAsync,
                    ["HasErrorHandling"] = hasErrorHandling,
                    ["PatternSubType"] = "GroupMessaging"
                }
            });
        }

        return patterns;
    }

    /// <summary>
    /// Detects user-specific messaging patterns
    /// </summary>
    private List<CodePattern> DetectUserMessaging(
        SyntaxNode root,
        string filePath,
        string? context,
        string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Detect: SendToUserAsync, SendToUser
        var invocations = root.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(inv => inv.ToString().Contains("SendToUserAsync") || 
                         inv.ToString().Contains("SendToUser("));

        foreach (var invocation in invocations)
        {
            var lineNumber = sourceCode.Take(invocation.SpanStart).Count(c => c == '\n') + 1;
            var snippet = GetCodeSnippet(sourceCode, lineNumber, 5);

            var isAsync = invocation.ToString().Contains("Async");
            var hasErrorHandling = IsWithinTryCatch(invocation);

            patterns.Add(new CodePattern
            {
                Name = "WebPubSub_UserMessage",
                Type = PatternType.AzureWebPubSub,
                Category = PatternCategory.RealtimeMessaging,
                Implementation = isAsync ? "SendToUserAsync" : "SendToUser",
                Language = "C#",
                FilePath = filePath,
                LineNumber = lineNumber,
                EndLineNumber = lineNumber + invocation.ToString().Split('\n').Length - 1,
                Content = snippet,
                BestPractice = $"Sending message to all connections for a specific user{(isAsync ? " (async)" : "")}. Useful for user-specific notifications. {(hasErrorHandling ? "Has error handling." : "WARNING: Should use try-catch.")}",
                AzureBestPracticeUrl = AzureWebPubSubKeyConceptsUrl,
                Confidence = hasErrorHandling && isAsync ? 0.95f : 0.80f,
                Context = context ?? string.Empty,
                Metadata = new Dictionary<string, object>
                {
                    ["IsAsync"] = isAsync,
                    ["HasErrorHandling"] = hasErrorHandling,
                    ["PatternSubType"] = "UserMessaging"
                }
            });
        }

        return patterns;
    }

    /// <summary>
    /// Detects authentication and authorization patterns
    /// </summary>
    private List<CodePattern> DetectAuthentication(
        SyntaxNode root,
        string filePath,
        string? context,
        string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Detect: Azure AD / Entra ID authentication
        var identifierNames = root.DescendantNodes()
            .OfType<IdentifierNameSyntax>()
            .Where(id => id.ToString().Contains("DefaultAzureCredential") ||
                        id.ToString().Contains("ManagedIdentityCredential") ||
                        id.ToString().Contains("AzureWebPubSubServiceClient") && 
                        id.Parent?.ToString().Contains("TokenCredential") == true);

        foreach (var identifier in identifierNames)
        {
            var lineNumber = sourceCode.Take(identifier.SpanStart).Count(c => c == '\n') + 1;
            var snippet = GetCodeSnippet(sourceCode, lineNumber, 5);

            var usesManagedIdentity = identifier.ToString().Contains("ManagedIdentityCredential");
            var usesDefaultCredential = identifier.ToString().Contains("DefaultAzureCredential");

            patterns.Add(new CodePattern
            {
                Name = "WebPubSub_Authentication",
                Type = PatternType.AzureWebPubSub,
                Category = PatternCategory.Security,
                Implementation = usesManagedIdentity ? "ManagedIdentityCredential" : 
                               usesDefaultCredential ? "DefaultAzureCredential" : "TokenCredential",
                Language = "C#",
                FilePath = filePath,
                LineNumber = lineNumber,
                EndLineNumber = lineNumber + identifier.ToString().Split('\n').Length - 1,
                Content = snippet,
                BestPractice = $"Using Azure AD / Entra ID authentication for secure service access{(usesManagedIdentity ? " with Managed Identity (recommended for Azure-hosted apps)" : usesDefaultCredential ? " with DefaultAzureCredential (recommended for development)" : "")}",
                AzureBestPracticeUrl = AzureWebPubSubAuthUrl,
                Confidence = 0.90f,
                Context = context ?? string.Empty,
                Metadata = new Dictionary<string, object>
                {
                    ["UsesManagedIdentity"] = usesManagedIdentity,
                    ["UsesDefaultCredential"] = usesDefaultCredential,
                    ["PatternSubType"] = "Authentication"
                }
            });
        }

        // Detect: JWT token generation for clients
        var jwtPatterns = root.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(inv => inv.ToString().Contains("GetClientAccessUri") &&
                         (inv.ToString().Contains("userId") || inv.ToString().Contains("roles")));

        foreach (var invocation in jwtPatterns)
        {
            var lineNumber = sourceCode.Take(invocation.SpanStart).Count(c => c == '\n') + 1;
            var snippet = GetCodeSnippet(sourceCode, lineNumber, 5);

            var hasUserId = invocation.ToString().Contains("userId");
            var hasRoles = invocation.ToString().Contains("roles");

            patterns.Add(new CodePattern
            {
                Name = "WebPubSub_ClientTokenGeneration",
                Type = PatternType.AzureWebPubSub,
                Category = PatternCategory.Security,
                Implementation = "GetClientAccessUri",
                Language = "C#",
                FilePath = filePath,
                LineNumber = lineNumber,
                EndLineNumber = lineNumber + invocation.ToString().Split('\n').Length - 1,
                Content = snippet,
                BestPractice = $"Generating secure client access token with{(hasUserId ? " user ID" : "")}{(hasRoles ? " and roles" : "")} for authenticated WebSocket connections",
                AzureBestPracticeUrl = AzureWebPubSubAuthUrl,
                Confidence = 0.90f,
                Context = context ?? string.Empty,
                Metadata = new Dictionary<string, object>
                {
                    ["HasUserId"] = hasUserId,
                    ["HasRoles"] = hasRoles,
                    ["PatternSubType"] = "ClientAuthentication"
                }
            });
        }

        return patterns;
    }

    /// <summary>
    /// Detects event handler patterns (webhooks)
    /// </summary>
    private List<CodePattern> DetectEventHandlers(
        SyntaxNode root,
        string filePath,
        string? context,
        string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Detect: WebPubSubEventHandler, event processing
        var classes = root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .Where(c => c.BaseList?.Types.Any(t => t.ToString().Contains("WebPubSubEventHandler")) == true ||
                       c.Identifier.ToString().Contains("EventHandler"));

        foreach (var classDecl in classes)
        {
            var lineNumber = sourceCode.Take(classDecl.SpanStart).Count(c => c == '\n') + 1;
            var snippet = GetCodeSnippet(sourceCode, lineNumber, 10);

            // Check for event validation
            var hasValidation = classDecl.DescendantNodes()
                .OfType<MethodDeclarationSyntax>()
                .Any(m => m.Identifier.ToString().Contains("Validate") || 
                         m.Modifiers.Any(mod => mod.ToString() == "override"));

            patterns.Add(new CodePattern
            {
                Name = "WebPubSub_EventHandler",
                Type = PatternType.AzureWebPubSub,
                Category = PatternCategory.EventHandlers,
                Implementation = "WebPubSubEventHandler",
                Language = "C#",
                FilePath = filePath,
                LineNumber = lineNumber,
                EndLineNumber = lineNumber + classDecl.ToString().Split('\n').Length - 1,
                Content = snippet,
                BestPractice = $"Event handler for processing upstream WebPubSub events (connect, connected, disconnected, message).{(hasValidation ? " Includes event validation." : " WARNING: Should validate event signatures for security.")}",
                AzureBestPracticeUrl = AzureWebPubSubEventHandlerUrl,
                Confidence = hasValidation ? 0.90f : 0.75f,
                Context = context ?? string.Empty,
                Metadata = new Dictionary<string, object>
                {
                    ["HasValidation"] = hasValidation,
                    ["PatternSubType"] = "EventHandler"
                }
            });
        }

        // Detect webhook event processing methods - ENHANCED DETECTION
        var allMethods = root.DescendantNodes().OfType<MethodDeclarationSyntax>().ToList();
        
        var methods = allMethods.Where(m => 
            {
                // Check for HttpPost attribute
                var hasHttpPost = m.AttributeLists.Any(al => al.ToString().Contains("HttpPost"));
                if (!hasHttpPost) return false;
                
                // Check method signature, parameters, and body for WebPubSub keywords
                var methodSignature = m.ToFullString();
                var parameterTypes = m.ParameterList.Parameters.Select(p => p.Type?.ToString() ?? "").ToList();
                var methodBody = m.Body?.ToString() ?? "";
                
                // Look for WebPubSubEventRequest parameter type OR WebPubSub in method name/body
                var hasWebPubSubParam = parameterTypes.Any(pt => pt.Contains("WebPubSubEventRequest"));
                var hasWebPubSubInSignature = methodSignature.Contains("WebPubSub");
                var hasWebPubSubInBody = methodBody.Contains("WebPubSub") || methodBody.Contains("VerifySignature");
                
                return hasWebPubSubParam || hasWebPubSubInSignature || hasWebPubSubInBody;
            });

        foreach (var method in methods)
        {
            var lineNumber = sourceCode.Take(method.SpanStart).Count(c => c == '\n') + 1;
            var snippet = GetCodeSnippet(sourceCode, lineNumber, 10);

            var hasSignatureValidation = method.Body?.DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .Any(inv => inv.ToString().Contains("Validate") || 
                           inv.ToString().Contains("VerifySignature")) == true;

            patterns.Add(new CodePattern
            {
                Name = "WebPubSub_WebhookEndpoint",
                Type = PatternType.AzureWebPubSub,
                Category = PatternCategory.EventHandlers,
                Implementation = "HTTP Webhook",
                Language = "C#",
                FilePath = filePath,
                LineNumber = lineNumber,
                EndLineNumber = lineNumber + method.ToString().Split('\n').Length - 1,
                Content = snippet,
                BestPractice = $"Webhook endpoint for handling WebPubSub events.{(hasSignatureValidation ? " Validates event signatures for security." : " CRITICAL: Must validate event signatures to prevent spoofing attacks!")}",
                AzureBestPracticeUrl = AzureWebPubSubEventHandlerUrl,
                Confidence = hasSignatureValidation ? 0.95f : 0.70f,
                Context = context ?? string.Empty,
                Metadata = new Dictionary<string, object>
                {
                    ["HasSignatureValidation"] = hasSignatureValidation,
                    ["PatternSubType"] = "WebhookEndpoint"
                }
            });
        }

        return patterns;
    }

    /// <summary>
    /// Detects hub management patterns
    /// </summary>
    private List<CodePattern> DetectHubManagement(
        SyntaxNode root,
        string filePath,
        string? context,
        string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Detect hub parameter usage in service client
        var objectCreations = root.DescendantNodes()
            .OfType<ObjectCreationExpressionSyntax>()
            .Where(node => node.Type.ToString().Contains("WebPubSubServiceClient") &&
                          node.ArgumentList?.Arguments.Count >= 2);

        foreach (var creation in objectCreations)
        {
            var lineNumber = sourceCode.Take(creation.SpanStart).Count(c => c == '\n') + 1;
            var snippet = GetCodeSnippet(sourceCode, lineNumber, 5);

            patterns.Add(new CodePattern
            {
                Name = "WebPubSub_HubConfiguration",
                Type = PatternType.AzureWebPubSub,
                Category = PatternCategory.RealtimeMessaging,
                Implementation = "Hub",
                Language = "C#",
                FilePath = filePath,
                LineNumber = lineNumber,
                EndLineNumber = lineNumber + creation.ToString().Split('\n').Length - 1,
                Content = snippet,
                BestPractice = "Configuring WebPubSub hub for logical grouping of connections. Hubs isolate different scenarios in your application.",
                AzureBestPracticeUrl = AzureWebPubSubKeyConceptsUrl,
                Confidence = 0.90f,
                Context = context ?? string.Empty,
                Metadata = new Dictionary<string, object>
                {
                    ["PatternSubType"] = "HubManagement"
                }
            });
        }

        return patterns;
    }

    /// <summary>
    /// Detects connection token generation patterns
    /// </summary>
    private List<CodePattern> DetectConnectionTokenGeneration(
        SyntaxNode root,
        string filePath,
        string? context,
        string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Already covered in DetectConnectionManagement and DetectAuthentication
        // This is a specialized version focusing on token generation specifics

        var invocations = root.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(inv => inv.ToString().Contains("GetClientAccessUri"));

        foreach (var invocation in invocations)
        {
            var lineNumber = sourceCode.Take(invocation.SpanStart).Count(c => c == '\n') + 1;
            var snippet = GetCodeSnippet(sourceCode, lineNumber, 7);

            // Check for expiration time
            var hasExpiration = invocation.ToString().Contains("expiresAfter") || 
                               invocation.ToString().Contains("TimeSpan");

            // Check for roles/permissions
            var hasRoles = invocation.ToString().Contains("roles") || 
                          invocation.ToString().Contains("permission");

            patterns.Add(new CodePattern
            {
                Name = "WebPubSub_ConnectionToken",
                Type = PatternType.AzureWebPubSub,
                Category = PatternCategory.Security,
                Implementation = "GetClientAccessUri",
                Language = "C#",
                FilePath = filePath,
                LineNumber = lineNumber,
                EndLineNumber = lineNumber + invocation.ToString().Split('\n').Length - 1,
                Content = snippet,
                BestPractice = $"Generating client connection token.{(hasExpiration ? " Sets token expiration." : " WARNING: Should set token expiration time.")}{(hasRoles ? " Includes roles/permissions." : "")}",
                AzureBestPracticeUrl = AzureWebPubSubAuthUrl,
                Confidence = hasExpiration ? 0.90f : 0.75f,
                Context = context ?? string.Empty,
                Metadata = new Dictionary<string, object>
                {
                    ["HasExpiration"] = hasExpiration,
                    ["HasRoles"] = hasRoles,
                    ["PatternSubType"] = "TokenGeneration"
                }
            });
        }

        return patterns;
    }

    /// <summary>
    /// Detects connection lifecycle management
    /// </summary>
    private List<CodePattern> DetectConnectionLifecycle(
        SyntaxNode root,
        string filePath,
        string? context,
        string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Detect connection close, reconnection handling
        var methods = root.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .Where(m => m.Identifier.ToString().ToLower().Contains("reconnect") ||
                       m.Identifier.ToString().ToLower().Contains("disconnect") ||
                       m.Body?.DescendantNodes()
                           .OfType<InvocationExpressionSyntax>()
                           .Any(inv => inv.ToString().Contains("CloseClientConnection")) == true);

        foreach (var method in methods)
        {
            var lineNumber = sourceCode.Take(method.SpanStart).Count(c => c == '\n') + 1;
            var snippet = GetCodeSnippet(sourceCode, lineNumber, 10);

            var hasRetry = method.Body?.DescendantNodes()
                .Any(n => n.ToString().Contains("retry") || 
                         n.ToString().Contains("Polly") ||
                         n.ToString().Contains("while")) == true;

            var hasLogging = method.Body?.DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .Any(inv => inv.ToString().Contains("Log")) == true;

            patterns.Add(new CodePattern
            {
                Name = "WebPubSub_ConnectionLifecycle",
                Type = PatternType.AzureWebPubSub,
                Category = PatternCategory.ConnectionManagement,
                Implementation = "Connection Lifecycle",
                Language = "C#",
                FilePath = filePath,
                LineNumber = lineNumber,
                EndLineNumber = lineNumber + method.ToString().Split('\n').Length - 1,
                Content = snippet,
                BestPractice = $"Managing connection lifecycle (connect/disconnect/reconnect).{(hasRetry ? " Includes retry logic." : " Consider adding retry logic for resilience.")}{(hasLogging ? " Has logging." : " Should add logging for diagnostics.")}",
                AzureBestPracticeUrl = AzureWebPubSubBestPracticesUrl,
                Confidence = (hasRetry && hasLogging) ? 0.95f : 0.80f,
                Context = context ?? string.Empty,
                Metadata = new Dictionary<string, object>
                {
                    ["HasRetry"] = hasRetry,
                    ["HasLogging"] = hasLogging,
                    ["PatternSubType"] = "ConnectionLifecycle"
                }
            });
        }

        return patterns;
    }

    // Helper methods

    private string GetCodeSnippet(string sourceCode, int lineNumber, int contextLines)
    {
        var lines = sourceCode.Split('\n');
        var startLine = Math.Max(0, lineNumber - contextLines - 1);
        var endLine = Math.Min(lines.Length, lineNumber + contextLines);
        
        return string.Join('\n', lines.Skip(startLine).Take(endLine - startLine));
    }

    private bool IsWithinTryCatch(SyntaxNode node)
    {
        var parent = node.Parent;
        while (parent != null)
        {
            if (parent is TryStatementSyntax)
                return true;
            parent = parent.Parent;
        }
        return false;
    }
}

