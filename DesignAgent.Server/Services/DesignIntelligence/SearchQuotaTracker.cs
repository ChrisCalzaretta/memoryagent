using System.Collections.Concurrent;

namespace DesignAgent.Server.Services.DesignIntelligence;

/// <summary>
/// Tracks API quota usage for search providers to intelligently rotate before hitting limits
/// </summary>
public class SearchQuotaTracker
{
    private readonly ConcurrentDictionary<string, ProviderQuota> _quotas = new();
    private readonly ILogger<SearchQuotaTracker> _logger;

    // Provider limits (from free tiers)
    private static readonly Dictionary<string, QuotaLimit> _providerLimits = new()
    {
        ["google"] = new QuotaLimit { Daily = 100, Monthly = 3000, ResetHour = 0 },
        ["brave"] = new QuotaLimit { Daily = null, Monthly = 2000, ResetHour = 0 },
        ["bing"] = new QuotaLimit { Daily = 100, Monthly = 3000, ResetHour = 0 },
        ["serper"] = new QuotaLimit { Daily = null, Monthly = 5000, ResetHour = 0 },
        // HTML scrapers have no limits
        ["duckduckgo"] = new QuotaLimit { Daily = null, Monthly = null, ResetHour = 0 }
    };

    public SearchQuotaTracker(ILogger<SearchQuotaTracker> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Check if provider has quota remaining
    /// </summary>
    public bool HasQuotaRemaining(string provider)
    {
        provider = provider.ToLower();
        
        // HTML scrapers always available
        if (provider == "duckduckgo" || !_providerLimits.ContainsKey(provider))
            return true;

        var quota = GetOrCreateQuota(provider);
        var limit = _providerLimits[provider];

        // Check daily limit
        if (limit.Daily.HasValue && quota.DailyCalls >= limit.Daily.Value)
        {
            _logger.LogWarning("Provider {Provider} has hit daily limit: {Calls}/{Limit}", 
                provider, quota.DailyCalls, limit.Daily.Value);
            return false;
        }

        // Check monthly limit
        if (limit.Monthly.HasValue && quota.MonthlyCalls >= limit.Monthly.Value)
        {
            _logger.LogWarning("Provider {Provider} has hit monthly limit: {Calls}/{Limit}", 
                provider, quota.MonthlyCalls, limit.Monthly.Value);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Record a successful API call
    /// </summary>
    public void RecordCall(string provider)
    {
        provider = provider.ToLower();
        
        if (!_providerLimits.ContainsKey(provider))
            return;

        var quota = GetOrCreateQuota(provider);
        
        quota.DailyCalls++;
        quota.MonthlyCalls++;
        quota.LastCallTime = DateTime.UtcNow;

        _logger.LogDebug("Provider {Provider} usage: Daily={Daily}, Monthly={Monthly}", 
            provider, quota.DailyCalls, quota.MonthlyCalls);
    }

    /// <summary>
    /// Get remaining calls for a provider
    /// </summary>
    public (int? daily, int? monthly) GetRemainingCalls(string provider)
    {
        provider = provider.ToLower();
        
        if (!_providerLimits.ContainsKey(provider))
            return (null, null);

        var quota = GetOrCreateQuota(provider);
        var limit = _providerLimits[provider];

        int? dailyRemaining = limit.Daily.HasValue 
            ? Math.Max(0, limit.Daily.Value - quota.DailyCalls) 
            : null;
            
        int? monthlyRemaining = limit.Monthly.HasValue 
            ? Math.Max(0, limit.Monthly.Value - quota.MonthlyCalls) 
            : null;

        return (dailyRemaining, monthlyRemaining);
    }

    /// <summary>
    /// Reset quotas based on time (called periodically)
    /// </summary>
    public void ResetExpiredQuotas()
    {
        var now = DateTime.UtcNow;
        
        foreach (var (provider, quota) in _quotas)
        {
            var limit = _providerLimits[provider];
            
            // Reset daily at midnight UTC
            if (quota.LastResetDate.Date < now.Date)
            {
                _logger.LogInformation("Resetting daily quota for {Provider}: {OldCalls} calls", 
                    provider, quota.DailyCalls);
                quota.DailyCalls = 0;
                quota.LastResetDate = now;
            }

            // Reset monthly on 1st of month
            if (quota.LastResetMonth.Year < now.Year || quota.LastResetMonth.Month < now.Month)
            {
                _logger.LogInformation("Resetting monthly quota for {Provider}: {OldCalls} calls", 
                    provider, quota.MonthlyCalls);
                quota.MonthlyCalls = 0;
                quota.LastResetMonth = now;
            }
        }
    }

    /// <summary>
    /// Get or create quota tracker for provider
    /// </summary>
    private ProviderQuota GetOrCreateQuota(string provider)
    {
        return _quotas.GetOrAdd(provider, _ => new ProviderQuota
        {
            Provider = provider,
            DailyCalls = 0,
            MonthlyCalls = 0,
            LastResetDate = DateTime.UtcNow,
            LastResetMonth = DateTime.UtcNow,
            LastCallTime = null
        });
    }

    private class ProviderQuota
    {
        public string Provider { get; set; } = "";
        public int DailyCalls { get; set; }
        public int MonthlyCalls { get; set; }
        public DateTime LastResetDate { get; set; }
        public DateTime LastResetMonth { get; set; }
        public DateTime? LastCallTime { get; set; }
    }

    private class QuotaLimit
    {
        public int? Daily { get; set; }
        public int? Monthly { get; set; }
        public int ResetHour { get; set; }
    }
}

