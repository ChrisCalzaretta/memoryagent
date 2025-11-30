using MemoryAgent.Server.Models;
using System.Text.RegularExpressions;

namespace MemoryAgent.Server.CodeAnalysis;

public partial class VBNetPatternDetector
{
    /// <summary>
    /// Detects Publisher-Subscriber messaging patterns in VB.NET
    /// Covers: Azure Service Bus, Event Grid, Event Hubs, .NET Events, Delegates
    /// </summary>
    private List<CodePattern> DetectPublisherSubscriberPatterns(string sourceCode, string[] lines, string filePath, string? context)
    {
        var patterns = new List<CodePattern>();
        const string AzurePubSubUrl = "https://learn.microsoft.com/en-us/azure/architecture/patterns/publisher-subscriber";

        // Pattern 1: Azure Service Bus Topics
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];

            if (Regex.IsMatch(line, @"New\s+ServiceBusClient|ServiceBusSender|ServiceBusReceiver", RegexOptions.IgnoreCase))
            {
                var pattern = CreatePattern(
                    name: "ServiceBus_Client",
                    type: PatternType.PublisherSubscriber,
                    category: PatternCategory.Reliability,
                    implementation: "AzureServiceBus",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 5),
                    bestPractice: "Azure Service Bus for reliable pub/sub messaging",
                    azureUrl: AzurePubSubUrl,
                    context: context
                );
                
                pattern.Metadata["messaging_technology"] = "Azure Service Bus";
                pattern.Metadata["pattern_type"] = "pub-sub";
                patterns.Add(pattern);
            }

            // Publishing to Service Bus
            if (Regex.IsMatch(line, @"SendMessageAsync|SendMessagesAsync", RegexOptions.IgnoreCase))
            {
                var pattern = CreatePattern(
                    name: "ServiceBus_Publisher",
                    type: PatternType.PublisherSubscriber,
                    category: PatternCategory.Reliability,
                    implementation: "ServiceBusPublisher",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 3),
                    bestPractice: "Publishing messages to Azure Service Bus",
                    azureUrl: AzurePubSubUrl,
                    context: context
                );
                
                pattern.Metadata["role"] = "publisher";
                patterns.Add(pattern);
            }

            // Receiving from Service Bus
            if (Regex.IsMatch(line, @"ReceiveMessageAsync|ReceiveMessagesAsync|ProcessMessageAsync", RegexOptions.IgnoreCase))
            {
                var pattern = CreatePattern(
                    name: "ServiceBus_Subscriber",
                    type: PatternType.PublisherSubscriber,
                    category: PatternCategory.Reliability,
                    implementation: "ServiceBusSubscriber",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 3),
                    bestPractice: "Subscribing to Azure Service Bus messages",
                    azureUrl: AzurePubSubUrl,
                    context: context
                );
                
                pattern.Metadata["role"] = "subscriber";
                patterns.Add(pattern);
            }
        }

        // Pattern 2: Azure Event Grid
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];

            if (Regex.IsMatch(line, @"EventGridPublisherClient|EventGridClient", RegexOptions.IgnoreCase))
            {
                var pattern = CreatePattern(
                    name: "EventGrid_Publisher",
                    type: PatternType.PublisherSubscriber,
                    category: PatternCategory.Performance,
                    implementation: "AzureEventGrid",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 5),
                    bestPractice: "Azure Event Grid for event-driven architecture",
                    azureUrl: AzurePubSubUrl,
                    context: context
                );
                
                pattern.Metadata["messaging_technology"] = "Azure Event Grid";
                pattern.Metadata["pattern_type"] = "event-driven";
                patterns.Add(pattern);
            }

            // Event Grid trigger attribute
            if (Regex.IsMatch(line, @"<EventGridTrigger>", RegexOptions.IgnoreCase))
            {
                var pattern = CreatePattern(
                    name: "EventGrid_Subscriber",
                    type: PatternType.PublisherSubscriber,
                    category: PatternCategory.Performance,
                    implementation: "EventGridTrigger",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 3),
                    bestPractice: "Event Grid subscriber using Azure Functions",
                    azureUrl: AzurePubSubUrl,
                    context: context
                );
                
                pattern.Metadata["role"] = "subscriber";
                pattern.Metadata["serverless"] = true;
                patterns.Add(pattern);
            }
        }

        // Pattern 3: Azure Event Hubs
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];

            if (Regex.IsMatch(line, @"EventHubProducerClient|EventHubClient", RegexOptions.IgnoreCase))
            {
                var pattern = CreatePattern(
                    name: "EventHub_Producer",
                    type: PatternType.PublisherSubscriber,
                    category: PatternCategory.Performance,
                    implementation: "AzureEventHubs",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 5),
                    bestPractice: "Azure Event Hubs for high-throughput event streaming",
                    azureUrl: AzurePubSubUrl,
                    context: context
                );
                
                pattern.Metadata["messaging_technology"] = "Azure Event Hubs";
                pattern.Metadata["pattern_type"] = "event-streaming";
                pattern.Metadata["high_throughput"] = true;
                patterns.Add(pattern);
            }

            if (Regex.IsMatch(line, @"EventProcessorClient|EventHubConsumerClient", RegexOptions.IgnoreCase))
            {
                var pattern = CreatePattern(
                    name: "EventHub_Consumer",
                    type: PatternType.PublisherSubscriber,
                    category: PatternCategory.Performance,
                    implementation: "EventHubConsumer",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 5),
                    bestPractice: "Consuming events from Azure Event Hubs",
                    azureUrl: AzurePubSubUrl,
                    context: context
                );
                
                pattern.Metadata["role"] = "consumer";
                patterns.Add(pattern);
            }
        }

        // Pattern 4: .NET Events and Delegates
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];

            // Event declaration
            if (Regex.IsMatch(line, @"Public\s+Event\s+\w+\s+As\s+EventHandler", RegexOptions.IgnoreCase))
            {
                var pattern = CreatePattern(
                    name: "DotNet_Event",
                    type: PatternType.PublisherSubscriber,
                    category: PatternCategory.General,
                    implementation: "EventHandler",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 3),
                    bestPractice: ".NET event for in-process pub/sub",
                    azureUrl: AzurePubSubUrl,
                    context: context
                );
                
                pattern.Metadata["pattern_type"] = "event";
                pattern.Metadata["in_process"] = true;
                patterns.Add(pattern);
            }

            // RaiseEvent
            if (Regex.IsMatch(line, @"RaiseEvent\s+\w+", RegexOptions.IgnoreCase))
            {
                var pattern = CreatePattern(
                    name: "DotNet_RaiseEvent",
                    type: PatternType.PublisherSubscriber,
                    category: PatternCategory.General,
                    implementation: "RaiseEvent",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 3),
                    bestPractice: "Raising .NET event to notify subscribers",
                    azureUrl: AzurePubSubUrl,
                    context: context
                );
                
                pattern.Metadata["role"] = "publisher";
                patterns.Add(pattern);
            }

            // AddHandler
            if (Regex.IsMatch(line, @"AddHandler\s+\w+", RegexOptions.IgnoreCase))
            {
                var pattern = CreatePattern(
                    name: "DotNet_AddHandler",
                    type: PatternType.PublisherSubscriber,
                    category: PatternCategory.General,
                    implementation: "AddHandler",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 3),
                    bestPractice: "Subscribing to .NET event",
                    azureUrl: AzurePubSubUrl,
                    context: context
                );
                
                pattern.Metadata["role"] = "subscriber";
                patterns.Add(pattern);
            }
        }

        // Pattern 5: MassTransit (VB.NET support)
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];

            if (Regex.IsMatch(line, @"IBus\.Publish|IBus\.Send", RegexOptions.IgnoreCase))
            {
                var pattern = CreatePattern(
                    name: "MassTransit_Publisher",
                    type: PatternType.PublisherSubscriber,
                    category: PatternCategory.Reliability,
                    implementation: "MassTransit",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 5),
                    bestPractice: "MassTransit message publishing",
                    azureUrl: AzurePubSubUrl,
                    context: context
                );
                
                pattern.Metadata["messaging_technology"] = "MassTransit";
                pattern.Metadata["library"] = "MassTransit";
                patterns.Add(pattern);
            }

            // IConsumer implementation
            if (Regex.IsMatch(line, @"Implements\s+IConsumer", RegexOptions.IgnoreCase))
            {
                var pattern = CreatePattern(
                    name: "MassTransit_Consumer",
                    type: PatternType.PublisherSubscriber,
                    category: PatternCategory.Reliability,
                    implementation: "MassTransitConsumer",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 5),
                    bestPractice: "MassTransit consumer for message handling",
                    azureUrl: AzurePubSubUrl,
                    context: context
                );
                
                pattern.Metadata["role"] = "consumer";
                patterns.Add(pattern);
            }
        }

        // Pattern 6: NServiceBus (VB.NET support)
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];

            if (Regex.IsMatch(line, @"IEndpointInstance\.Publish|IMessageSession\.Publish", RegexOptions.IgnoreCase))
            {
                var pattern = CreatePattern(
                    name: "NServiceBus_Publisher",
                    type: PatternType.PublisherSubscriber,
                    category: PatternCategory.Reliability,
                    implementation: "NServiceBus",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 5),
                    bestPractice: "NServiceBus event publishing",
                    azureUrl: AzurePubSubUrl,
                    context: context
                );
                
                pattern.Metadata["messaging_technology"] = "NServiceBus";
                pattern.Metadata["library"] = "NServiceBus";
                patterns.Add(pattern);
            }

            // IHandleMessages implementation
            if (Regex.IsMatch(line, @"Implements\s+IHandleMessages", RegexOptions.IgnoreCase))
            {
                var pattern = CreatePattern(
                    name: "NServiceBus_Handler",
                    type: PatternType.PublisherSubscriber,
                    category: PatternCategory.Reliability,
                    implementation: "NServiceBusHandler",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 5),
                    bestPractice: "NServiceBus message handler",
                    azureUrl: AzurePubSubUrl,
                    context: context
                );
                
                pattern.Metadata["role"] = "subscriber";
                patterns.Add(pattern);
            }
        }

        return patterns;
    }
}

