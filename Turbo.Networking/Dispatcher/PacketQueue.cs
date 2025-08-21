using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using Turbo.Core.Configuration;
using Turbo.Core.Networking.Dispatcher;
using Turbo.Core.Packets.Messages;

namespace Turbo.Networking.Dispatcher;

public class PacketQueue : IPacketQueue, IDisposable
{
    private readonly Channel<PacketEnvelope>[] _shards;
    private readonly int _mask;
    private readonly ShardMetrics[] _shardMetrics;
    private volatile bool _disposed;

    // Performance optimizations
    private volatile int _cachedApproxDepth;
    private long _lastDepthUpdate;
    private readonly object _depthLock = new();

    private sealed class ShardMetrics
    {
        private long _totalEnqueued;
        private long _totalDropped;

        public long TotalEnqueued => _totalEnqueued;
        public long TotalDropped => _totalDropped;
        public DateTime LastEnqueueTime { get; set; } = DateTime.UtcNow;
        public int CurrentDepth => _channel?.Reader.Count ?? 0;
        public int Capacity { get; }

        private readonly Channel<PacketEnvelope> _channel;

        public ShardMetrics(Channel<PacketEnvelope> channel, int capacity)
        {
            _channel = channel;
            Capacity = capacity;
        }

        public void IncrementEnqueued() => Interlocked.Increment(ref _totalEnqueued);

        public void IncrementDropped() => Interlocked.Increment(ref _totalDropped);
    }

    public PacketQueue(IEmulatorConfig config)
    {
        if (config?.Network?.DispatcherOptions == null)
            throw new ArgumentNullException(
                nameof(config),
                "Network dispatcher options cannot be null"
            );

        // Choose a power-of-two shard count (usually == worker count)
        var workers = Math.Max(1, config.Network.DispatcherOptions.Workers);
        ShardCount = 1;
        while (ShardCount < workers)
            ShardCount <<= 1;
        _mask = ShardCount - 1;

        // Calculate per-shard capacity with validation
        var globalCapacity = Math.Max(1024, config.Network.DispatcherOptions.GlobalQueueCapacity);
        var perShardCapacity = Math.Max(256, globalCapacity / ShardCount);

        // Initialize shards and metrics
        _shards = new Channel<PacketEnvelope>[ShardCount];
        _shardMetrics = new ShardMetrics[ShardCount];

        for (int i = 0; i < ShardCount; i++)
        {
            var channelOptions = new BoundedChannelOptions(perShardCapacity)
            {
                // Use DropWrite for better performance under pressure
                FullMode = BoundedChannelFullMode.DropWrite,
                SingleReader = true, // single consumer per shard
                SingleWriter = false, // multiple producers enqueue into this shard
                AllowSynchronousContinuations = false, // Better for high-throughput scenarios
            };

            _shards[i] = Channel.CreateBounded<PacketEnvelope>(channelOptions);
            _shardMetrics[i] = new ShardMetrics(_shards[i], perShardCapacity);
        }
    }

    public int ShardCount { get; }

    private int ShardOf(PacketEnvelope env)
    {
        // Enhanced hash function for better distribution
        var h = env.ChannelId.GetHashCode();
        unchecked
        {
            h ^= (h << 13);
            h ^= (h >> 17);
            h ^= (h << 5);
        }
        return h & _mask;
    }

    public bool TryEnqueue(PacketEnvelope envelope)
    {
        if (_disposed)
            return false;

        var shardIndex = ShardOf(envelope);
        var shard = _shards[shardIndex];
        var metrics = _shardMetrics[shardIndex];

        // TryWrite avoids async state machines in hot path
        var success = shard.Writer.TryWrite(envelope);

        // Update metrics
        if (success)
        {
            metrics.IncrementEnqueued();
            metrics.LastEnqueueTime = DateTime.UtcNow;
        }
        else
        {
            metrics.IncrementDropped();
        }

        return success;
    }

    public int ApproxDepth
    {
        get
        {
            // Cache depth calculation for performance (update max every 100ms)
            var now = Environment.TickCount64;
            if (now - _lastDepthUpdate > 100)
            {
                lock (_depthLock)
                {
                    if (now - _lastDepthUpdate > 100)
                    {
                        _cachedApproxDepth = _shards.Sum(s => s.Reader.Count);
                        _lastDepthUpdate = now;
                    }
                }
            }
            return _cachedApproxDepth;
        }
    }

    public IAsyncEnumerable<PacketEnvelope> ReadShardAsync(int shardIndex, CancellationToken ct)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(PacketQueue));

        if (shardIndex < 0 || shardIndex >= ShardCount)
            throw new ArgumentOutOfRangeException(nameof(shardIndex));

        return _shards[shardIndex].Reader.ReadAllAsync(ct);
    }

    /// <summary>
    /// Gets detailed statistics for all shards.
    /// </summary>
    public PacketQueueStats GetStatistics()
    {
        var shardStats = new ShardStats[ShardCount];
        long totalEnqueued = 0;
        long totalDropped = 0;
        int totalDepth = 0;

        for (int i = 0; i < ShardCount; i++)
        {
            var metrics = _shardMetrics[i];
            shardStats[i] = new ShardStats
            {
                ShardIndex = i,
                TotalEnqueued = metrics.TotalEnqueued,
                TotalDropped = metrics.TotalDropped,
                CurrentDepth = metrics.CurrentDepth,
                Capacity = metrics.Capacity,
                LastEnqueueTime = metrics.LastEnqueueTime,
            };

            totalEnqueued += metrics.TotalEnqueued;
            totalDropped += metrics.TotalDropped;
            totalDepth += metrics.CurrentDepth;
        }

        return new PacketQueueStats
        {
            TotalEnqueued = totalEnqueued,
            TotalDropped = totalDropped,
            CurrentDepth = totalDepth,
            ShardCount = ShardCount,
            ShardStats = shardStats,
        };
    }

    /// <summary>
    /// Checks if the queue is healthy (not overloaded).
    /// </summary>
    public bool IsHealthy()
    {
        if (_disposed)
            return false;

        var stats = GetStatistics();
        var dropRate =
            stats.TotalEnqueued > 0 ? (double)stats.TotalDropped / stats.TotalEnqueued : 0;

        // Consider unhealthy if drop rate > 5% or any shard is > 90% full
        return dropRate < 0.05 && stats.ShardStats.All(s => s.CurrentDepth < s.Capacity * 0.9);
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        // Complete all writers to signal shutdown
        for (int i = 0; i < ShardCount; i++)
        {
            _shards[i].Writer.TryComplete();
        }
    }
}

/// <summary>
/// Statistics for a single shard.
/// </summary>
public class ShardStats
{
    public int ShardIndex { get; init; }
    public long TotalEnqueued { get; init; }
    public long TotalDropped { get; init; }
    public int CurrentDepth { get; init; }
    public int Capacity { get; init; }
    public DateTime LastEnqueueTime { get; init; }
}

/// <summary>
/// Overall queue statistics.
/// </summary>
public class PacketQueueStats
{
    public long TotalEnqueued { get; init; }
    public long TotalDropped { get; init; }
    public int CurrentDepth { get; init; }
    public int ShardCount { get; init; }
    public ShardStats[] ShardStats { get; init; }
}
