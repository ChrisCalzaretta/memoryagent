using MemoryAgent.Server.Models;
using System.Text.RegularExpressions;

namespace MemoryAgent.Server.CodeAnalysis;

public partial class JavaScriptPatternDetector
{
    /// <summary>
    /// Detects Publisher-Subscriber messaging patterns in JavaScript/TypeScript
    /// Covers: EventEmitter, RxJS, WebSocket, Server-Sent Events, Message Queues, Cloud Messaging
    /// </summary>
    private List<CodePattern> DetectPublisherSubscriberPatterns(string sourceCode, string[] lines, string filePath, string? context, string language)
    {
        var patterns = new List<CodePattern>();
        const string AzurePubSubUrl = "https://learn.microsoft.com/en-us/azure/architecture/patterns/publisher-subscriber";

        // Pattern 1: Node.js EventEmitter (Built-in pub/sub)
        for (int i = 0; i < lines.Length; i++)
        {
            if (Regex.IsMatch(lines[i], @"new\s+EventEmitter\(\)|extends\s+EventEmitter", RegexOptions.IgnoreCase))
            {
                var pattern = CreatePattern(
                    name: "NodeJS_EventEmitter",
                    type: PatternType.PublisherSubscriber,
                    category: PatternCategory.General,
                    implementation: "EventEmitter",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 5),
                    bestPractice: "Node.js EventEmitter for in-process pub/sub",
                    azureUrl: AzurePubSubUrl,
                    context: context,
                    language: language
                );
                
                pattern.Metadata["messaging_technology"] = "Node.js Events";
                pattern.Metadata["pattern_type"] = "event-emitter";
                pattern.Metadata["in_process"] = true;
                patterns.Add(pattern);
            }

            // EventEmitter .emit() calls
            if (Regex.IsMatch(lines[i], @"\.emit\s*\(", RegexOptions.IgnoreCase))
            {
                var pattern = CreatePattern(
                    name: "EventEmitter_Publish",
                    type: PatternType.PublisherSubscriber,
                    category: PatternCategory.General,
                    implementation: "EventEmitter.emit",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 3),
                    bestPractice: "Publishing events using EventEmitter",
                    azureUrl: AzurePubSubUrl,
                    context: context,
                    language: language
                );
                
                pattern.Metadata["role"] = "publisher";
                pattern.Confidence = 0.8f;
                patterns.Add(pattern);
            }

            // EventEmitter .on() / .addListener()
            if (Regex.IsMatch(lines[i], @"\.on\s*\(|\.addListener\s*\(|\.once\s*\(", RegexOptions.IgnoreCase))
            {
                var pattern = CreatePattern(
                    name: "EventEmitter_Subscribe",
                    type: PatternType.PublisherSubscriber,
                    category: PatternCategory.General,
                    implementation: "EventEmitter.on",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 3),
                    bestPractice: "Subscribing to events using EventEmitter",
                    azureUrl: AzurePubSubUrl,
                    context: context,
                    language: language
                );
                
                pattern.Metadata["role"] = "subscriber";
                pattern.Confidence = 0.8f;
                patterns.Add(pattern);
            }
        }

        // Pattern 2: RxJS Observable (Reactive pub/sub)
        for (int i = 0; i < lines.Length; i++)
        {
            if (Regex.IsMatch(lines[i], @"new\s+Observable\(|Observable\.create|from\(|of\(|interval\(|fromEvent\(", RegexOptions.IgnoreCase))
            {
                var pattern = CreatePattern(
                    name: "RxJS_Observable",
                    type: PatternType.PublisherSubscriber,
                    category: PatternCategory.Performance,
                    implementation: "RxJS",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 5),
                    bestPractice: "RxJS Observable for reactive event streaming",
                    azureUrl: AzurePubSubUrl,
                    context: context,
                    language: language
                );
                
                pattern.Metadata["messaging_technology"] = "RxJS";
                pattern.Metadata["pattern_type"] = "reactive";
                pattern.Metadata["role"] = "publisher";
                pattern.Metadata["library"] = "rxjs";
                patterns.Add(pattern);
            }

            // Subject (hot observable)
            if (Regex.IsMatch(lines[i], @"new\s+(Subject|BehaviorSubject|ReplaySubject|AsyncSubject)\(", RegexOptions.IgnoreCase))
            {
                var pattern = CreatePattern(
                    name: "RxJS_Subject",
                    type: PatternType.PublisherSubscriber,
                    category: PatternCategory.Performance,
                    implementation: "RxJS",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 5),
                    bestPractice: "RxJS Subject for multicast pub/sub",
                    azureUrl: AzurePubSubUrl,
                    context: context,
                    language: language
                );
                
                pattern.Metadata["hot_observable"] = true;
                pattern.Metadata["multicast"] = true;
                pattern.Metadata["subject_type"] = "Subject";
                patterns.Add(pattern);
            }

            // .subscribe() - check for RxJS context (Observable, Subject, rxjs import)
            if (Regex.IsMatch(lines[i], @"\.subscribe\s*\(", RegexOptions.IgnoreCase) && 
                (sourceCode.Contains("Observable") || sourceCode.Contains("Subject") || sourceCode.Contains("rxjs")))
            {
                var pattern = CreatePattern(
                    name: "RxJS_Subscriber",
                    type: PatternType.PublisherSubscriber,
                    category: PatternCategory.Performance,
                    implementation: "RxJS",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 3),
                    bestPractice: "Subscribing to RxJS Observable",
                    azureUrl: AzurePubSubUrl,
                    context: context,
                    language: language
                );
                
                pattern.Metadata["role"] = "subscriber";
                pattern.Confidence = 0.7f;
                patterns.Add(pattern);
            }
        }

        // Pattern 3: WebSocket (Real-time bidirectional messaging)
        for (int i = 0; i < lines.Length; i++)
        {
            if (Regex.IsMatch(lines[i], @"new\s+WebSocket\(|ws://|wss://", RegexOptions.IgnoreCase))
            {
                var pattern = CreatePattern(
                    name: "WebSocket_Connection",
                    type: PatternType.PublisherSubscriber,
                    category: PatternCategory.Performance,
                    implementation: "WebSocket",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 5),
                    bestPractice: "WebSocket for real-time bidirectional messaging",
                    azureUrl: AzurePubSubUrl,
                    context: context,
                    language: language
                );
                
                pattern.Metadata["messaging_technology"] = "WebSocket";
                pattern.Metadata["real_time"] = true;
                pattern.Metadata["bidirectional"] = true;
                patterns.Add(pattern);
            }

            // Socket.IO (WebSocket library)
            if (Regex.IsMatch(lines[i], @"io\(|socket\.emit|socket\.on", RegexOptions.IgnoreCase))
            {
                var pattern = CreatePattern(
                    name: "SocketIO_PubSub",
                    type: PatternType.PublisherSubscriber,
                    category: PatternCategory.Performance,
                    implementation: "Socket.IO",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 5),
                    bestPractice: "Socket.IO for real-time pub/sub messaging",
                    azureUrl: AzurePubSubUrl,
                    context: context,
                    language: language
                );
                
                pattern.Metadata["library"] = "socket.io";
                pattern.Metadata["supports_rooms"] = true;
                patterns.Add(pattern);
            }
        }

        // Pattern 4: Server-Sent Events (SSE)
        for (int i = 0; i < lines.Length; i++)
        {
            if (Regex.IsMatch(lines[i], @"new\s+EventSource\(", RegexOptions.IgnoreCase))
            {
                var pattern = CreatePattern(
                    name: "ServerSentEvents_Subscriber",
                    type: PatternType.PublisherSubscriber,
                    category: PatternCategory.Performance,
                    implementation: "EventSource",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 5),
                    bestPractice: "Server-Sent Events for server-to-client streaming",
                    azureUrl: AzurePubSubUrl,
                    context: context,
                    language: language
                );
                
                pattern.Metadata["messaging_technology"] = "SSE";
                pattern.Metadata["pattern_type"] = "server-push";
                pattern.Metadata["role"] = "subscriber";
                patterns.Add(pattern);
            }
        }

        // Pattern 5: Azure Service Bus (Node.js SDK)
        for (int i = 0; i < lines.Length; i++)
        {
            if (Regex.IsMatch(lines[i], @"ServiceBusClient|createSender|createReceiver", RegexOptions.IgnoreCase))
            {
                var pattern = CreatePattern(
                    name: "AzureServiceBus_Client",
                    type: PatternType.PublisherSubscriber,
                    category: PatternCategory.Reliability,
                    implementation: "@azure/service-bus",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 5),
                    bestPractice: "Azure Service Bus for reliable messaging",
                    azureUrl: AzurePubSubUrl,
                    context: context,
                    language: language
                );
                
                pattern.Metadata["messaging_technology"] = "Azure Service Bus";
                pattern.Metadata["library"] = "@azure/service-bus";
                pattern.Metadata["cloud_native"] = true;
                patterns.Add(pattern);
            }
        }

        // Pattern 6: RabbitMQ (amqplib)
        for (int i = 0; i < lines.Length; i++)
        {
            if (Regex.IsMatch(lines[i], @"amqp\.connect|channel\.publish|channel\.consume|channel\.assertExchange", RegexOptions.IgnoreCase))
            {
                var pattern = CreatePattern(
                    name: "RabbitMQ_AMQP",
                    type: PatternType.PublisherSubscriber,
                    category: PatternCategory.Reliability,
                    implementation: "amqplib",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 5),
                    bestPractice: "RabbitMQ AMQP for message queue pub/sub",
                    azureUrl: AzurePubSubUrl,
                    context: context,
                    language: language
                );
                
                pattern.Metadata["messaging_technology"] = "RabbitMQ";
                pattern.Metadata["library"] = "amqplib";
                pattern.Metadata["protocol"] = "AMQP";
                patterns.Add(pattern);
            }
        }

        // Pattern 7: Redis Pub/Sub (Node.js)
        for (int i = 0; i < lines.Length; i++)
        {
            if (Regex.IsMatch(lines[i], @"redis\.publish|redis\.subscribe|redis\.psubscribe", RegexOptions.IgnoreCase))
            {
                var pattern = CreatePattern(
                    name: "Redis_PubSub",
                    type: PatternType.PublisherSubscriber,
                    category: PatternCategory.Performance,
                    implementation: "redis",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 5),
                    bestPractice: "Redis pub/sub for lightweight message broadcasting",
                    azureUrl: AzurePubSubUrl,
                    context: context,
                    language: language
                );
                
                pattern.Metadata["messaging_technology"] = "Redis";
                pattern.Metadata["library"] = "redis";
                pattern.Metadata["pattern_matching"] = lines[i].Contains("psubscribe");
                patterns.Add(pattern);
            }
        }

        // Pattern 8: Kafka (KafkaJS)
        for (int i = 0; i < lines.Length; i++)
        {
            if (Regex.IsMatch(lines[i], @"new\s+Kafka\(|producer\.send|consumer\.subscribe|consumer\.run", RegexOptions.IgnoreCase))
            {
                var pattern = CreatePattern(
                    name: "Kafka_Streaming",
                    type: PatternType.PublisherSubscriber,
                    category: PatternCategory.Performance,
                    implementation: "kafkajs",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 5),
                    bestPractice: "Apache Kafka for event streaming and pub/sub",
                    azureUrl: AzurePubSubUrl,
                    context: context,
                    language: language
                );
                
                pattern.Metadata["messaging_technology"] = "Apache Kafka";
                pattern.Metadata["library"] = "kafkajs";
                pattern.Metadata["high_throughput"] = true;
                pattern.Metadata["event_streaming"] = true;
                patterns.Add(pattern);
            }
        }

        // Pattern 9: Custom Event Bus / Event Aggregator
        for (int i = 0; i < lines.Length; i++)
        {
            if (Regex.IsMatch(lines[i], @"class\s+\w*EventBus|class\s+\w*EventAggregator|class\s+\w*MessageBus", RegexOptions.IgnoreCase))
            {
                var pattern = CreatePattern(
                    name: "Custom_EventBus",
                    type: PatternType.PublisherSubscriber,
                    category: PatternCategory.General,
                    implementation: "Custom",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 10),
                    bestPractice: "Custom event bus implementation for application-level pub/sub",
                    azureUrl: AzurePubSubUrl,
                    context: context,
                    language: language
                );
                
                pattern.Metadata["pattern_type"] = "event-bus";
                pattern.Metadata["custom_implementation"] = true;
                patterns.Add(pattern);
            }
        }

        return patterns;
    }
}

