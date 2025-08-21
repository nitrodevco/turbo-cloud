using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DotNetty.Transport.Channels;
using Microsoft.Extensions.Logging;
using Turbo.Core.Configuration;
using Turbo.Core.Networking.Dispatcher;
using Turbo.Core.Networking.Pipeline;
using Turbo.Core.Networking.Session;
using Turbo.Core.Packets;
using Turbo.Core.Packets.Messages;

namespace Turbo.Networking.Pipelines;

public class IngressPipeline(
    IRateLimiter limiter,
    IPacketQueue queue,
    IBackpressureManager backpressure,
    ISessionManager sessions,
    IEmulatorConfig config,
    ILogger<IngressPipeline> logger
) : IIngressPipeline
{
    private readonly ConcurrentDictionary<IChannelId, int> _pending = new();
    private readonly ConcurrentDictionary<IChannelId, IngressToken> _tokenCache = new();

    // Performance optimization for backpressure
    private long _lastBackpressureUpdate;
    private readonly object _backpressureLock = new();

    // Metrics
    private long _totalPackets;
    private long _rejectedRateLimit;
    private long _rejectedBusy;
    private long _rejectedServerBusy;
    private long _hardExceeds;

    public bool TryAccept(PacketEnvelope env, out PacketRejectType reject, out IngressToken token)
    {
        Interlocked.Increment(ref _totalPackets);
        token = null;
        reject = PacketRejectType.None;

        try
        {
            // Input validation
            if (env.ChannelId == null)
            {
                logger.LogWarning("Received packet with null ChannelId");
                reject = PacketRejectType.InvalidInput;
                return false;
            }

            // Obtain a reusable token for this channel
            token = _tokenCache.GetOrAdd(env.ChannelId, static id => new IngressToken(id));

            // 1) Rate limit check
            if (!limiter.TryAcquire(env.ChannelId, out var hardExceeded))
            {
                reject = PacketRejectType.RateLimited;
                Interlocked.Increment(ref _rejectedRateLimit);

                if (hardExceeded)
                {
                    Interlocked.Increment(ref _hardExceeds);
                    logger.LogInformation(
                        "Kicking session {ChannelId} for rate limit violations",
                        env.ChannelId
                    );

                    // Fire-and-forget with error handling
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await sessions.KickSessionAsync(
                                env.ChannelId,
                                SessionKickType.RateLimited
                            );
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(
                                ex,
                                "Failed to kick session {ChannelId}",
                                env.ChannelId
                            );
                        }
                    });
                }
                else
                {
                    logger.LogDebug(
                        "Rate limited packet from {ChannelId}, header={Header}",
                        env.ChannelId,
                        env.Msg.Header
                    );
                }

                return false;
            }

            // 2) Per-session pending check
            var pending = _pending.AddOrUpdate(env.ChannelId, 1, static (_, v) => v + 1);
            var maxPending = config.Network.DispatcherOptions.MaxPendingPerSession;

            if (pending > maxPending)
            {
                reject = PacketRejectType.Busy;
                Interlocked.Increment(ref _rejectedBusy);

                logger.LogDebug(
                    "Session {ChannelId} has too many pending packets: {Pending}/{Max}",
                    env.ChannelId,
                    pending,
                    maxPending
                );

                // Decrement pending count since we're rejecting
                _pending.AddOrUpdate(env.ChannelId, 0, static (_, v) => Math.Max(0, v - 1));
                return false;
            }

            // 3) Global queue capacity check
            if (!queue.TryEnqueue(env))
            {
                reject = PacketRejectType.ServerBusy;
                Interlocked.Increment(ref _rejectedServerBusy);

                logger.LogDebug(
                    "Global queue full, rejecting packet from {ChannelId}",
                    env.ChannelId
                );

                // Decrement pending count since we're rejecting
                _pending.AddOrUpdate(env.ChannelId, 0, static (_, v) => Math.Max(0, v - 1));
                return false;
            }

            // 4) Backpressure management (throttled to avoid excessive calls)
            UpdateBackpressureIfNeeded();

            logger.LogTrace(
                "Accepted packet from {ChannelId}, header={Header}, pending={Pending}",
                env.ChannelId,
                env.Msg.Header,
                pending
            );

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error in TryAccept for {ChannelId}", env.ChannelId);
            reject = PacketRejectType.InternalError;
            return false;
        }
    }

    private void UpdateBackpressureIfNeeded()
    {
        var now = Environment.TickCount64;

        // Only update backpressure every 250ms to reduce overhead
        if (now - _lastBackpressureUpdate > 250)
        {
            lock (_backpressureLock)
            {
                if (now - _lastBackpressureUpdate > 250)
                {
                    try
                    {
                        backpressure.UpdateDepth(queue.ApproxDepth, sessions);
                        _lastBackpressureUpdate = now;
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error updating backpressure");
                    }
                }
            }
        }
    }

    public void OnProcessed(IngressToken token)
    {
        try
        {
            if (token?.ChannelId == null)
            {
                logger.LogWarning("OnProcessed called with null token or ChannelId");
                return;
            }

            var left = _pending.AddOrUpdate(
                token.ChannelId,
                0,
                static (_, v) => Math.Max(0, v - 1)
            );

            logger.LogTrace(
                "Processed packet for {ChannelId}, {Left} pending",
                token.ChannelId,
                left
            );

            // Reset rate limiter state when no packets are pending
            if (left == 0)
            {
                limiter.Reset(token.ChannelId);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in OnProcessed for {ChannelId}", token?.ChannelId);
        }
    }

    public void Reset(IChannelId channelId)
    {
        try
        {
            if (channelId == null)
            {
                logger.LogWarning("Reset called with null ChannelId");
                return;
            }

            logger.LogDebug("Resetting pipeline state for {ChannelId}", channelId);

            limiter.Reset(channelId);
            _pending.TryRemove(channelId, out var pendingCount);
            _tokenCache.TryRemove(channelId, out _);

            if (pendingCount > 0)
            {
                logger.LogInformation(
                    "Reset {ChannelId} with {PendingCount} pending packets",
                    channelId,
                    pendingCount
                );
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error resetting pipeline for {ChannelId}", channelId);
        }
    }

    public IngressToken GetOrCreateToken(IChannelId channelId)
    {
        try
        {
            if (channelId == null)
            {
                throw new ArgumentNullException(nameof(channelId));
            }

            return _tokenCache.GetOrAdd(channelId, static id => new IngressToken(id));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting/creating token for {ChannelId}", channelId);
            throw;
        }
    }

    /// <summary>
    /// Gets comprehensive statistics about ingress pipeline performance.
    /// </summary>
    public IngressPipelineStats GetStatistics()
    {
        var activeSessions = _pending.Count;
        var totalPendingPackets = 0;
        var maxPendingPerSession = 0;

        foreach (var kvp in _pending)
        {
            var pending = kvp.Value;
            totalPendingPackets += pending;
            if (pending > maxPendingPerSession)
                maxPendingPerSession = pending;
        }

        return new IngressPipelineStats
        {
            TotalPackets = _totalPackets,
            RejectedRateLimit = _rejectedRateLimit,
            RejectedBusy = _rejectedBusy,
            RejectedServerBusy = _rejectedServerBusy,
            HardExceeds = _hardExceeds,
            ActiveSessions = activeSessions,
            TotalPendingPackets = totalPendingPackets,
            MaxPendingPerSession = maxPendingPerSession,
            CachedTokens = _tokenCache.Count,
            AcceptanceRate =
                _totalPackets > 0
                    ? (double)(
                        _totalPackets - _rejectedRateLimit - _rejectedBusy - _rejectedServerBusy
                    ) / _totalPackets
                    : 1.0,
        };
    }

    /// <summary>
    /// Checks if the ingress pipeline is operating within healthy parameters.
    /// </summary>
    public bool IsHealthy()
    {
        var stats = GetStatistics();

        // Consider unhealthy if:
        // - Acceptance rate < 80%
        // - Any session has excessive pending packets
        var maxAllowedPending = config.Network.DispatcherOptions.MaxPendingPerSession;

        return stats.AcceptanceRate > 0.8 && stats.MaxPendingPerSession < maxAllowedPending * 0.9;
    }

    /// <summary>
    /// Gets detailed information about a specific session's state.
    /// </summary>
    public SessionPipelineInfo GetSessionInfo(IChannelId channelId)
    {
        if (channelId == null)
            throw new ArgumentNullException(nameof(channelId));

        var pendingCount = _pending.TryGetValue(channelId, out var pending) ? pending : 0;
        var hasToken = _tokenCache.ContainsKey(channelId);

        return new SessionPipelineInfo
        {
            ChannelId = channelId,
            PendingPackets = pendingCount,
            HasCachedToken = hasToken,
            MaxAllowedPending = config.Network.DispatcherOptions.MaxPendingPerSession,
        };
    }

    /// <summary>
    /// Performs cleanup of stale entries to prevent memory leaks.
    /// </summary>
    public int CleanupStaleEntries()
    {
        var removed = 0;
        var staleSessions = new List<IChannelId>();

        // Find sessions with zero pending packets (potentially disconnected)
        foreach (var kvp in _pending)
        {
            if (kvp.Value == 0)
            {
                staleSessions.Add(kvp.Key);
            }
        }

        // Remove stale entries
        foreach (var channelId in staleSessions)
        {
            if (_pending.TryRemove(channelId, out _))
            {
                _tokenCache.TryRemove(channelId, out _);
                removed++;
            }
        }

        if (removed > 0)
        {
            logger.LogDebug("Cleaned up {Count} stale pipeline entries", removed);
        }

        return removed;
    }
}

/// <summary>
/// Statistics for ingress pipeline performance and health.
/// </summary>
public class IngressPipelineStats
{
    public long TotalPackets { get; init; }
    public long RejectedRateLimit { get; init; }
    public long RejectedBusy { get; init; }
    public long RejectedServerBusy { get; init; }
    public long HardExceeds { get; init; }
    public int ActiveSessions { get; init; }
    public int TotalPendingPackets { get; init; }
    public int MaxPendingPerSession { get; init; }
    public int CachedTokens { get; init; }
    public double AcceptanceRate { get; init; }
}

/// <summary>
/// Information about a specific session's pipeline state.
/// </summary>
public class SessionPipelineInfo
{
    public IChannelId ChannelId { get; init; }
    public int PendingPackets { get; init; }
    public bool HasCachedToken { get; init; }
    public int MaxAllowedPending { get; init; }
}
