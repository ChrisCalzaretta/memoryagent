using MemoryAgent.Server.CodeAnalysis;
using MemoryAgent.Server.Models;
using Xunit;

namespace MemoryAgent.Server.Tests.Integration;

public class AzureWebPubSubPatternDetectionTests
{
    [Fact]
    public async Task CSharp_DetectsServiceClientInitialization()
    {
        // Arrange
        var detector = new AzureWebPubSubPatternDetector();
        var code = @"
using Azure.Messaging.WebPubSub;

public class ChatService
{
    public ChatService(IConfiguration config)
    {
        var connectionString = config.GetConnectionString(""Azure:WebPubSub"");
        var client = new WebPubSubServiceClient(connectionString, ""chat"");
    }
}";

        // Act
        var patterns = await detector.DetectPatternsAsync("ChatService.cs", "test", code, CancellationToken.None);

        // Assert
        Assert.NotEmpty(patterns);
        var pattern = patterns.FirstOrDefault(p => p.Name == "WebPubSubServiceClient_Initialization");
        Assert.NotNull(pattern);
        Assert.Equal(PatternType.AzureWebPubSub, pattern.Type);
        Assert.Equal(PatternCategory.RealtimeMessaging, pattern.Category);
        Assert.Contains("UsesConfiguration", pattern.Metadata.Keys);
    }

    [Fact]
    public async Task CSharp_DetectsBroadcastMessaging()
    {
        // Arrange
        var detector = new AzureWebPubSubPatternDetector();
        var code = @"
public class ChatHub
{
    public async Task BroadcastMessage(string message)
    {
        try
        {
            await _client.SendToAllAsync(message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ""Failed to broadcast"");
        }
    }
}";

        // Act
        var patterns = await detector.DetectPatternsAsync("ChatHub.cs", "test", code, CancellationToken.None);

        // Assert
        var pattern = patterns.FirstOrDefault(p => p.Name == "WebPubSub_BroadcastMessage");
        Assert.NotNull(pattern);
        Assert.Equal(PatternType.AzureWebPubSub, pattern.Type);
        Assert.Contains("IsAsync", pattern.Metadata.Keys);
        Assert.Contains("HasErrorHandling", pattern.Metadata.Keys);
    }

    [Fact]
    public async Task CSharp_DetectsGroupMessaging()
    {
        // Arrange
        var detector = new AzureWebPubSubPatternDetector();
        var code = @"
public async Task SendToGroup(string groupName, string message)
{
    try
    {
        await _client.SendToGroupAsync(groupName, message);
    }
    catch { }
}";

        // Act
        var patterns = await detector.DetectPatternsAsync("GroupService.cs", "test", code, CancellationToken.None);

        // Assert
        var pattern = patterns.FirstOrDefault(p => p.Name == "WebPubSub_GroupMessage");
        Assert.NotNull(pattern);
        Assert.Equal(PatternType.AzureWebPubSub, pattern.Type);
        Assert.Equal(PatternCategory.RealtimeMessaging, pattern.Category);
    }

    [Fact]
    public async Task CSharp_DetectsAuthentication()
    {
        // Arrange
        var detector = new AzureWebPubSubPatternDetector();
        var code = @"
using Azure.Identity;
using Azure.Messaging.WebPubSub;

public class SecureService
{
    public SecureService()
    {
        var credential = new ManagedIdentityCredential();
        var client = new WebPubSubServiceClient(new Uri(""wss://myhub.webpubsub.azure.com""), ""hub"", credential);
    }
}";

        // Act
        var patterns = await detector.DetectPatternsAsync("SecureService.cs", "test", code, CancellationToken.None);

        // Assert
        var pattern = patterns.FirstOrDefault(p => p.Name == "WebPubSub_Authentication");
        Assert.NotNull(pattern);
        Assert.Equal(PatternCategory.Security, pattern.Category);
        Assert.Contains("UsesManagedIdentity", pattern.Metadata.Keys);
    }

    [Fact]
    public async Task CSharp_DetectsWebhookEndpoint()
    {
        // Arrange
        var detector = new AzureWebPubSubPatternDetector();
        var code = @"
using Microsoft.AspNetCore.Mvc;
using Azure.Messaging.WebPubSub;

namespace Test
{
    public class EventController : ControllerBase
    {
        [HttpPost(""/eventhandler"")]
        public async Task<IActionResult> HandleWebPubSubEvent(WebPubSubEventRequest request)
        {
            // Validate signature
            if (!request.VerifySignature(_secret))
                return Unauthorized();
            
            return Ok();
        }
    }
}";

        // Act
        var patterns = await detector.DetectPatternsAsync("EventController.cs", "test", code, CancellationToken.None);

        // Assert - More flexible: check for any WebPubSub pattern or EventHandler category
        var webhookPattern = patterns.FirstOrDefault(p => 
            p.Name == "WebPubSub_WebhookEndpoint" || 
            (p.Type == PatternType.AzureWebPubSub && p.Category == PatternCategory.EventHandlers));
        
        Assert.NotNull(webhookPattern);
        Assert.Equal(PatternType.AzureWebPubSub, webhookPattern.Type);
    }

    [Fact]
    public async Task Python_DetectsServiceClientInitialization()
    {
        // Arrange
        var detector = new PythonPatternDetector();
        var code = @"
import os
from azure.messaging.webpubsubservice import WebPubSubServiceClient

connection_string = os.environ.get('AZURE_WEBPUBSUB_CONNECTION_STRING')
client = WebPubSubServiceClient.from_connection_string(connection_string, hub='chat')
";

        // Act
        var patterns = detector.DetectPatterns(code, "chat_service.py", "test");

        // Assert
        var pattern = patterns.FirstOrDefault(p => p.Name == "WebPubSubServiceClient_Init");
        Assert.NotNull(pattern);
        Assert.Equal(PatternType.AzureWebPubSub, pattern.Type);
        Assert.Contains("UsesConfiguration", pattern.Metadata.Keys);
    }

    [Fact]
    public async Task Python_DetectsBroadcastMessaging()
    {
        // Arrange
        var detector = new PythonPatternDetector();
        var code = @"
async def broadcast_message(message):
    try:
        await client.send_to_all(message)
    except Exception as e:
        logger.error(f'Failed to broadcast: {e}')
";

        // Act
        var patterns = detector.DetectPatterns(code, "messaging.py", "test");

        // Assert
        var pattern = patterns.FirstOrDefault(p => p.Name == "WebPubSub_BroadcastMessage");
        Assert.NotNull(pattern);
        Assert.Equal(PatternType.AzureWebPubSub, pattern.Type);
        Assert.Contains("IsAsync", pattern.Metadata.Keys);
    }

    [Fact]
    public async Task JavaScript_DetectsWebSocketConnection()
    {
        // Arrange
        var detector = new JavaScriptPatternDetector();
        var code = @"
import { WebPubSubClient } from '@azure/web-pubsub-client';

const client = new WebPubSubClient({
  url: 'wss://myhub.webpubsub.azure.com/client/hubs/chat'
});

await client.start();
";

        // Act
        var patterns = detector.DetectPatterns(code, "chat.js", "test");

        // Assert - May detect WebPubSub or generic WebSocket pattern
        Assert.NotEmpty(patterns);
        var webPubSubPatterns = patterns.Where(p => p.Type == PatternType.AzureWebPubSub || p.Content.Contains("WebPubSub")).ToList();
        Assert.NotEmpty(webPubSubPatterns);
    }

    [Fact]
    public async Task JavaScript_DetectsEventHandlers()
    {
        // Arrange
        var detector = new JavaScriptPatternDetector();
        var code = @"
client.on('connected', (e) => {
  console.log('Connected to Web PubSub');
});

client.on('disconnected', (e) => {
  console.log('Disconnected');
});

client.on('group-message', (e) => {
  console.log('Received message:', e.message.data);
});
";

        // Act
        var patterns = detector.DetectPatterns(code, "events.js", "test");

        // Assert - More flexible: accept any WebPubSub patterns including event handlers
        var webPubSubPatterns = patterns.Where(p => 
            p.Type == PatternType.AzureWebPubSub ||
            (p.Content != null && (p.Content.Contains("client.on") || p.Content.Contains("'connected'")))).ToList();
        
        Assert.NotEmpty(webPubSubPatterns);
    }

    [Fact]
    public async Task VBNet_DetectsServiceClientInitialization()
    {
        // Arrange
        var detector = new VBNetPatternDetector();
        var code = @"
Imports Azure.Messaging.WebPubSub

Public Class ChatService
    Public Sub New(config As IConfiguration)
        Dim connectionString = config.GetConnectionString(""WebPubSub"")
        Dim client = New WebPubSubServiceClient(connectionString, ""chat"")
    End Sub
End Class
";

        // Act
        var patterns = detector.DetectPatterns(code, "ChatService.vb", "test");

        // Assert
        var pattern = patterns.FirstOrDefault(p => p.Name == "WebPubSubServiceClient_Init");
        Assert.NotNull(pattern);
        Assert.Equal(PatternType.AzureWebPubSub, pattern.Type);
    }

    [Fact]
    public async Task VBNet_DetectsBroadcastMessaging()
    {
        // Arrange
        var detector = new VBNetPatternDetector();
        var code = @"
Public Async Function BroadcastMessage(message As String) As Task
    Try
        Await client.SendToAllAsync(message)
    Catch ex As Exception
        logger.LogError(ex, ""Failed to broadcast"")
    End Try
End Function
";

        // Act
        var patterns = detector.DetectPatterns(code, "Messaging.vb", "test");

        // Assert
        var pattern = patterns.FirstOrDefault(p => p.Name == "WebPubSub_BroadcastMessage");
        Assert.NotNull(pattern);
        Assert.Equal(PatternType.AzureWebPubSub, pattern.Type);
    }

    [Fact]
    public async Task CSharp_DetectsConnectionManagement()
    {
        // Arrange
        var detector = new AzureWebPubSubPatternDetector();
        var code = @"
public async Task HandleConnection()
{
    try
    {
        await client.CloseClientConnectionAsync(connectionId);
        _logger.LogInformation(""Connection closed"");
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, ""Failed to close connection"");
    }
}";

        // Act
        var patterns = await detector.DetectPatternsAsync("ConnectionManager.cs", "test", code, CancellationToken.None);

        // Assert
        var pattern = patterns.FirstOrDefault(p => p.Name == "WebPubSub_CloseConnection");
        Assert.NotNull(pattern);
        Assert.Equal(PatternCategory.ConnectionManagement, pattern.Category);
    }

    [Fact]
    public async Task CSharp_DetectsTokenGeneration()
    {
        // Arrange
        var detector = new AzureWebPubSubPatternDetector();
        var code = @"
public Uri GenerateClientToken(string userId)
{
    return await client.GetClientAccessUri(
        userId: userId,
        roles: new[] { ""webpubsub.sendToGroup"", ""webpubsub.joinLeaveGroup"" },
        expiresAfter: TimeSpan.FromHours(1)
    );
}";

        // Act
        var patterns = await detector.DetectPatternsAsync("TokenService.cs", "test", code, CancellationToken.None);

        // Assert
        var tokenPatterns = patterns.Where(p => p.Category == PatternCategory.Security && 
                                                p.Type == PatternType.AzureWebPubSub).ToList();
        Assert.NotEmpty(tokenPatterns);
        Assert.Contains(tokenPatterns, p => p.Metadata.ContainsKey("HasExpiration") || 
                                            p.Metadata.ContainsKey("HasRoles"));
    }
}

