using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using DotNetty.Transport.Channels;
using Turbo.Core.Configuration;
using Turbo.Core.Networking.Dispatcher;

namespace Turbo.Networking.Dispatcher;

public class TokenBucketRateLimiter : IRateLimiter
{
    private readonly TokenBucket<IChannelId> _bucket;
    private readonly ConcurrentDictionary<IChannelId, ViolationTracker> _violations = new();
    private readonly int _maxViolationsBeforeKick;
    private readonly TimeSpan _violationWindow;

    // Metrics
    private long _totalAcquireAttempts;
    private long _totalRateLimited;
    private long _totalHardExceeded;

    private sealed class ViolationTracker
    {
        private int _violations;
        private DateTime _firstViolation = DateTime.UtcNow;
        private DateTime _lastViolation = DateTime.UtcNow;

        public int Violations => _violations;
        public DateTime FirstViolation => _firstViolation;
        public DateTime LastViolation => _lastViolation;

        public bool AddViolation(TimeSpan window)
        {
            var now = DateTime.UtcNow;

            // Reset counter if outside violation window
            if (now - _firstViolation > window)
            {
                _violations = 1;
                _firstViolation = now;
                _lastViolation = now;
                return false;
            }

            _violations++;
            _lastViolation = now;
            return true;
        }

        public void Reset()
        {
            _violations = 0;
            _firstViolation = DateTime.UtcNow;
            _lastViolation = DateTime.UtcNow;
        }
    }

    // optional: wire your own "hard exceed" policy elsewhere if you want kicks
    public TokenBucketRateLimiter(IEmulatorConfig config)
    {
        if (config?.Network?.DispatcherOptions == null)
            throw new ArgumentNullException(
                nameof(config),
                "Network dispatcher options cannot be null"
            );

        // Validate configuration
        var capacity = Math.Max(1, config.Network.RateLimitCapacity);
        var refillPerSec = Math.Max(0.1, config.Network.RateLimitRefillPerSecond);
        var stripes = config.Network.RateLimitStripes <= 0 ? 64 : config.Network.RateLimitStripes;

        _maxViolationsBeforeKick = Math.Max(
            1,
            config.Network.DispatcherOptions.RateViolationsBeforeKick
        );
        _violationWindow = TimeSpan.FromMinutes(5); // 5-minute violation window

        _bucket = new TokenBucket<IChannelId>(
            capacity: capacity,
            refillPerSec: refillPerSec,
            stripes: stripes,
            cleanupThreshold: TimeSpan.FromMinutes(10) // Cleanup after 10 minutes of inactivity
        );
    }

    public bool TryAcquire(IChannelId channelId, out bool hardExceeded)
    {
        Interlocked.Increment(ref _totalAcquireAttempts);
        hardExceeded = false;

        var allowed = _bucket.TryTake(channelId, 1);

        if (!allowed)
        {
            Interlocked.Increment(ref _totalRateLimited);

            // Track violations for hard exceed detection
            var violations = _violations.GetOrAdd(channelId, _ => new ViolationTracker());
            violations.AddViolation(_violationWindow);

            // Check if this channel has exceeded the violation threshold
            if (violations.Violations >= _maxViolationsBeforeKick)
            {
                hardExceeded = true;
                Interlocked.Increment(ref _totalHardExceeded);

                // Reset violations after kicking (give them a fresh start)
                violations.Reset();
            }
        }
        else
        {
            // Reset violations on successful acquire (good behavior)
            if (_violations.TryGetValue(channelId, out var violations))
            {
                violations.Reset();
            }
        }

        return allowed;
    }

    public void Reset(IChannelId channelId)
    {
        _bucket.Reset(channelId);
        _violations.TryRemove(channelId, out _);
    }

    /// <summary>
    /// Gets comprehensive statistics about rate limiting performance.
    /// </summary>
    public RateLimiterStats GetStatistics()
    {
        var currentViolations = 0;
        var channelsWithViolations = 0;

        foreach (var kvp in _violations)
        {
            var violations = kvp.Value;
            if (violations.Violations > 0)
            {
                channelsWithViolations++;
                currentViolations += violations.Violations;
            }
        }

        return new RateLimiterStats
        {
            TotalAcquireAttempts = _totalAcquireAttempts,
            TotalRateLimited = _totalRateLimited,
            TotalHardExceeded = _totalHardExceeded,
            CurrentViolations = currentViolations,
            ChannelsWithViolations = channelsWithViolations,
            ActiveChannels = _violations.Count,
            SuccessRate =
                _totalAcquireAttempts > 0
                    ? (double)(_totalAcquireAttempts - _totalRateLimited) / _totalAcquireAttempts
                    : 1.0,
        };
    }

    /// <summary>
    /// Checks if the rate limiter is functioning within normal parameters.
    /// </summary>
    public bool IsHealthy()
    {
        var stats = GetStatistics();

        // Consider unhealthy if:
        // - Success rate < 50% (too aggressive limiting)
        // - More than 10% of channels have violations
        var violationRate =
            stats.ActiveChannels > 0
                ? (double)stats.ChannelsWithViolations / stats.ActiveChannels
                : 0;

        return stats.SuccessRate > 0.5 && violationRate < 0.1;
    }

    /// <summary>
    /// Gets violation details for a specific channel.
    /// </summary>
    public ViolationInfo GetChannelViolations(IChannelId channelId)
    {
        if (!_violations.TryGetValue(channelId, out var tracker))
            return null;

        return new ViolationInfo
        {
            ChannelId = channelId,
            Violations = tracker.Violations,
            FirstViolation = tracker.FirstViolation,
            LastViolation = tracker.LastViolation,
            IsAtRisk = tracker.Violations >= _maxViolationsBeforeKick * 0.8, // 80% of threshold
        };
    }

    /// <summary>
    /// Manually cleans up stale violation trackers.
    /// </summary>
    public int CleanupStaleViolations()
    {
        var removed = 0;
        var cutoff = DateTime.UtcNow - _violationWindow - TimeSpan.FromMinutes(5); // Extra buffer

        var toRemove = new List<IChannelId>();
        foreach (var kvp in _violations)
        {
            if (kvp.Value.LastViolation < cutoff)
            {
                toRemove.Add(kvp.Key);
            }
        }

        foreach (var channelId in toRemove)
        {
            if (_violations.TryRemove(channelId, out _))
                removed++;
        }

        return removed;
    }
}

/// <summary>
/// Statistics for rate limiter performance and health.
/// </summary>
public class RateLimiterStats
{
    public long TotalAcquireAttempts { get; init; }
    public long TotalRateLimited { get; init; }
    public long TotalHardExceeded { get; init; }
    public int CurrentViolations { get; init; }
    public int ChannelsWithViolations { get; init; }
    public int ActiveChannels { get; init; }
    public double SuccessRate { get; init; }
}

/// <summary>
/// Violation information for a specific channel.
/// </summary>
public class ViolationInfo
{
    public IChannelId ChannelId { get; init; }
    public int Violations { get; init; }
    public DateTime FirstViolation { get; init; }
    public DateTime LastViolation { get; init; }
    public bool IsAtRisk { get; init; }
}
