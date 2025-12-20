using System.Collections.Concurrent;

namespace MemoryRouter.Server.Services;

/// <summary>
/// Tracks actual tool execution performance for statistical learning
/// Learns which tools are fast vs slow based on real usage patterns
/// </summary>
public class PerformanceTracker : IPerformanceTracker
{
    private readonly ConcurrentDictionary<string, List<PerformanceRecord>> _executionHistory = new();
    private readonly ILogger<PerformanceTracker> _logger;
    private const int MaxHistoryPerTool = 100; // Keep last 100 executions
    private const int MinSamplesForStatistics = 5; // Need 5+ samples for reliable stats

    public PerformanceTracker(ILogger<PerformanceTracker> logger)
    {
        _logger = logger;
    }

    public void RecordExecution(string toolName, long durationMs, Dictionary<string, object>? context = null)
    {
        var record = new PerformanceRecord
        {
            ToolName = toolName,
            DurationMs = durationMs,
            Timestamp = DateTime.UtcNow,
            Context = context
        };

        _executionHistory.AddOrUpdate(
            toolName,
            _ => new List<PerformanceRecord> { record },
            (_, existing) =>
            {
                lock (existing)
                {
                    existing.Add(record);
                    
                    // Keep only last N executions
                    if (existing.Count > MaxHistoryPerTool)
                    {
                        existing.RemoveAt(0);
                    }
                }
                return existing;
            });

        _logger.LogDebug("📊 Recorded: {Tool} took {Ms}ms (total samples: {Count})", 
            toolName, durationMs, _executionHistory[toolName].Count);
    }

    public bool HasSufficientData(string toolName)
    {
        return _executionHistory.TryGetValue(toolName, out var history) 
            && history.Count >= MinSamplesForStatistics;
    }

    public PerformanceStats? GetStats(string toolName)
    {
        if (!_executionHistory.TryGetValue(toolName, out var history) || history.Count == 0)
        {
            return null;
        }

        List<long> durations;
        lock (history)
        {
            durations = history.Select(r => r.DurationMs).ToList();
        }

        var sorted = durations.OrderBy(d => d).ToList();
        
        return new PerformanceStats
        {
            ToolName = toolName,
            SampleSize = sorted.Count,
            MinDurationMs = sorted.First(),
            MaxDurationMs = sorted.Last(),
            AverageDurationMs = (long)sorted.Average(),
            MedianDurationMs = sorted[sorted.Count / 2],
            P90DurationMs = sorted[(int)(sorted.Count * 0.9)],
            P95DurationMs = sorted[(int)(sorted.Count * 0.95)],
            RecentTrend = CalculateTrend(history)
        };
    }

    public long GetAverageDurationMs(string toolName)
    {
        var stats = GetStats(toolName);
        return stats?.AverageDurationMs ?? 0;
    }

    public long GetP90DurationMs(string toolName)
    {
        var stats = GetStats(toolName);
        return stats?.P90DurationMs ?? 0;
    }

    private string CalculateTrend(List<PerformanceRecord> history)
    {
        if (history.Count < 10) return "insufficient_data";

        var recent5 = history.TakeLast(5).Average(r => r.DurationMs);
        var previous5 = history.Skip(history.Count - 10).Take(5).Average(r => r.DurationMs);

        var change = (recent5 - previous5) / previous5;

        return change switch
        {
            > 0.2 => "increasing",
            < -0.2 => "decreasing",
            _ => "stable"
        };
    }
}

public interface IPerformanceTracker
{
    void RecordExecution(string toolName, long durationMs, Dictionary<string, object>? context = null);
    bool HasSufficientData(string toolName);
    PerformanceStats? GetStats(string toolName);
    long GetAverageDurationMs(string toolName);
    long GetP90DurationMs(string toolName);
}

public class PerformanceRecord
{
    public required string ToolName { get; set; }
    public long DurationMs { get; set; }
    public DateTime Timestamp { get; set; }
    public Dictionary<string, object>? Context { get; set; }
}

public class PerformanceStats
{
    public required string ToolName { get; set; }
    public int SampleSize { get; set; }
    public long MinDurationMs { get; set; }
    public long MaxDurationMs { get; set; }
    public long AverageDurationMs { get; set; }
    public long MedianDurationMs { get; set; }
    public long P90DurationMs { get; set; }
    public long P95DurationMs { get; set; }
    public string RecentTrend { get; set; } = "stable";
}


