using System;
using System.Collections.Concurrent;
using DotNetty.Transport.Channels;
using Turbo.Core.Configuration;
using Turbo.Core.Networking.Dispatcher;

namespace Turbo.Networking.Dispatcher;

public class TokenBucketRateLimiter : IRateLimiter
{
    private readonly TokenBucket<IChannelId> _bucket;

    // optional: wire your own "hard exceed" policy elsewhere if you want kicks
    public TokenBucketRateLimiter(IEmulatorConfig config)
    {
        var stripes = config.Network.RateLimitStripes <= 0 ? 64 : config.Network.RateLimitStripes;
        _bucket = new TokenBucket<IChannelId>(
            capacity: config.Network.RateLimitCapacity,
            refillPerSec: config.Network.RateLimitRefillPerSecond,
            stripes: stripes
        );
    }

    public bool TryAcquire(IChannelId channelId, out bool hardExceeded)
    {
        var ok = _bucket.TryTake(channelId, 1);
        hardExceeded = false; // keep hard-exceeded decision outside the bucket (ingress counters)
        return ok;
    }

    public void Reset(IChannelId channelId) => _bucket.Reset(channelId);
}
