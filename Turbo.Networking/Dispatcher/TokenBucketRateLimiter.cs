namespace Turbo.Networking.Dispatcher;

using System.Collections.Concurrent;

using DotNetty.Transport.Channels;

using Turbo.Core.Configuration;
using Turbo.Core.Networking.Dispatcher;

public class TokenBucketRateLimiter(IEmulatorConfig options) : IRateLimiter
{
    private readonly TokenBucket<IChannelId> _bucket = new(options.Network.DispatcherOptions.RateCapacity, options.Network.DispatcherOptions.RateRefillPerSec);
    private readonly ConcurrentDictionary<IChannelId, int> _violations = new();

    public int MaxViolations { get; } = options.Network.DispatcherOptions.RateViolationsBeforeKick;

    public bool TryAcquire(IChannelId channelId, out bool exceededLimit)
    {
        if (_bucket.TryTake(channelId))
        {
            exceededLimit = false;
            return true;
        }

        var strikes = _violations.AddOrUpdate(channelId, 1, (_, v) => v + 1);
        exceededLimit = strikes >= MaxViolations;
        return false;
    }

    public void Reset(IChannelId channelId)
    {
        _bucket.Reset(channelId);
        _violations.TryRemove(channelId, out _);
    }
}
