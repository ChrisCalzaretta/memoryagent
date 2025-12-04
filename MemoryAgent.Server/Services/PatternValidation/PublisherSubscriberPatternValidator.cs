using MemoryAgent.Server.Models;
using System.Text.RegularExpressions;

namespace MemoryAgent.Server.Services.PatternValidation;

/// <summary>
/// Validator for Publisher-Subscriber messaging patterns (Azure Service Bus, Event Hubs, etc.)
/// </summary>
public class PublisherSubscriberPatternValidator : IPatternValidator
{
    public IEnumerable<PatternType> SupportedPatternTypes => new[] { PatternType.PublisherSubscriber };

    public PatternQualityResult Validate(CodePattern pattern)
    {
        var result = new PatternQualityResult
        {
            Pattern = pattern,
            Score = 10,
            SecurityScore = 10
        };

        // Check for message idempotency handling
        if (!Regex.IsMatch(pattern.Content, @"(idempotent|MessageId|SequenceNumber|DeduplicationId)", RegexOptions.IgnoreCase))
        {
            result.Issues.Add(new ValidationIssue
            {
                Severity = IssueSeverity.High,
                Category = IssueCategory.BestPractice,
                Message = "No idempotency handling detected - duplicate messages may cause data inconsistencies",
                ScoreImpact = 2,
                FixGuidance = "Implement message deduplication using MessageId or DeduplicationId to handle repeated messages"
            });
            result.Score -= 2;
        }

        // Check for error handling / dead letter queue
        if (!Regex.IsMatch(pattern.Content, @"(try|catch|DeadLetter|ErrorQueue|HandleError)", RegexOptions.IgnoreCase))
        {
            result.Issues.Add(new ValidationIssue
            {
                Severity = IssueSeverity.High,
                Category = IssueCategory.BestPractice,
                Message = "No error handling or dead-letter queue pattern detected",
                ScoreImpact = 3,
                FixGuidance = "Implement error handling and configure dead-letter queue for poison messages"
            });
            result.Score -= 3;
            result.SecurityScore -= 1;
        }

        // Check for message expiration/TTL
        if (pattern.Implementation.Contains("ServiceBus") || pattern.Implementation.Contains("EventHubs"))
        {
            if (!Regex.IsMatch(pattern.Content, @"(TimeToLive|TTL|Expir|MessageLifespan)", RegexOptions.IgnoreCase))
            {
                result.Issues.Add(new ValidationIssue
                {
                    Severity = IssueSeverity.Medium,
                    Category = IssueCategory.BestPractice,
                    Message = "No message expiration (TTL) configured - old messages may accumulate",
                    ScoreImpact = 1,
                    FixGuidance = "Set TimeToLive on messages to prevent stale data accumulation"
                });
                result.Score -= 1;
            }
        }

        // Check for subscription filtering (for topic-based patterns)
        if (pattern.Name.Contains("Topic") || pattern.Name.Contains("Subscription"))
        {
            if (!Regex.IsMatch(pattern.Content, @"(Filter|Rule|CorrelationFilter|SqlFilter)", RegexOptions.IgnoreCase))
            {
                result.Issues.Add(new ValidationIssue
                {
                    Severity = IssueSeverity.Low,
                    Category = IssueCategory.BestPractice,
                    Message = "No subscription filtering detected - subscribers receive all messages",
                    ScoreImpact = 1,
                    FixGuidance = "Implement subscription filters to reduce message processing overhead"
                });
                result.Score -= 1;
            }
        }

        // Check for message ordering concerns (Event Hubs, Service Bus sessions)
        if (pattern.Implementation.Contains("EventHubs"))
        {
            if (!Regex.IsMatch(pattern.Content, @"(PartitionKey|SessionId)", RegexOptions.IgnoreCase))
            {
                result.Issues.Add(new ValidationIssue
                {
                    Severity = IssueSeverity.Low,
                    Category = IssueCategory.BestPractice,
                    Message = "No partition key for ordering - messages may be processed out of order",
                    ScoreImpact = 1,
                    FixGuidance = "Use PartitionKey to ensure related messages are processed in order"
                });
                result.Score -= 1;
            }
        }

        // Check for retry policy
        if (!Regex.IsMatch(pattern.Content, @"(Retry|MaxDelivery|LockDuration)", RegexOptions.IgnoreCase))
        {
            result.Issues.Add(new ValidationIssue
            {
                Severity = IssueSeverity.Medium,
                Category = IssueCategory.Reliability,
                Message = "No retry policy configuration detected",
                ScoreImpact = 2,
                FixGuidance = "Configure retry policy with MaxDeliveryCount and LockDuration"
            });
            result.Score -= 2;
        }

        // Check for authentication/security
        if (pattern.Implementation.Contains("Azure"))
        {
            if (!Regex.IsMatch(pattern.Content, @"(TokenCredential|ManagedIdentity|ServiceBusClient\(.*credential)", RegexOptions.IgnoreCase))
            {
                result.Issues.Add(new ValidationIssue
                {
                    Severity = IssueSeverity.Critical,
                    Category = IssueCategory.Security,
                    Message = "No managed identity or secure authentication detected - may be using connection strings",
                    ScoreImpact = 3,
                    FixGuidance = "Use DefaultAzureCredential or ManagedIdentity instead of connection strings"
                });
                result.Score -= 3;
                result.SecurityScore -= 3;
            }
        }

        // Check for telemetry/logging
        if (!Regex.IsMatch(pattern.Content, @"(ILogger|Log\.|Telemetry|TrackEvent|ApplicationInsights)", RegexOptions.IgnoreCase))
        {
            result.Issues.Add(new ValidationIssue
            {
                Severity = IssueSeverity.Medium,
                Category = IssueCategory.BestPractice,
                Message = "No telemetry or logging detected for message processing",
                ScoreImpact = 1,
                FixGuidance = "Add logging for message processing events and errors"
            });
            result.Score -= 1;
        }

        // Check for proper consumer scaling (competing consumers pattern)
        if (pattern.Metadata.TryGetValue("role", out var role) && role?.ToString() == "consumer")
        {
            if (!Regex.IsMatch(pattern.Content, @"(PrefetchCount|MaxConcurrentCalls|ProcessorCount)", RegexOptions.IgnoreCase))
            {
                result.Issues.Add(new ValidationIssue
                {
                    Severity = IssueSeverity.Low,
                    Category = IssueCategory.Performance,
                    Message = "No concurrency configuration for message processing",
                    ScoreImpact = 1,
                    FixGuidance = "Configure PrefetchCount and MaxConcurrentCalls for optimal throughput"
                });
                result.Score -= 1;
            }
        }

        result.Score = Math.Max(0, result.Score);
        result.SecurityScore = Math.Max(0, result.SecurityScore);
        result.Summary = $"Publisher-Subscriber Pattern Quality: {result.Grade} ({result.Score}/10) | Security: {result.SecurityScore}/10";

        return result;
    }

    public PatternMigrationPath? GenerateMigrationPath(CodePattern pattern, bool includeCodeExample = true)
    {
        return null;
    }
}

