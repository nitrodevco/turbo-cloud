using System;
using System.Collections.Concurrent;
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

    public bool TryAccept(PacketEnvelope env, out PacketRejectType reject, out IngressToken token)
    {
        // Obtain a reusable token for this channel. If it does not exist,
        // GetOrAdd will create a new one. This eliminates the per-packet
        // allocation that previously occurred here.
        token = _tokenCache.GetOrAdd(env.ChannelId, static id => new IngressToken(id));
        reject = PacketRejectType.None;

        // 1) Rate limit
        if (!limiter.TryAcquire(env.ChannelId, out var hardExceeded))
        {
            reject = PacketRejectType.RateLimited;
            if (hardExceeded)
                _ = sessions.KickSessionAsync(env.ChannelId, SessionKickType.RateLimited);
            return false;
        }

        // 2) Per-session pending
        var pending = _pending.AddOrUpdate(env.ChannelId, 1, static (_, v) => v + 1);
        if (pending > config.Network.DispatcherOptions.MaxPendingPerSession)
        {
            reject = PacketRejectType.Busy;
            _pending.AddOrUpdate(env.ChannelId, 0, static (_, v) => Math.Max(0, v - 1));
            return false;
        }

        // 3) Global queue
        if (!queue.TryEnqueue(env))
        {
            reject = PacketRejectType.ServerBusy;
            _pending.AddOrUpdate(env.ChannelId, 0, static (_, v) => Math.Max(0, v - 1));
            return false;
        }

        // 4) Backpressure hint
        backpressure.UpdateDepth(queue.ApproxDepth, sessions);
        return true;
    }

    public void OnProcessed(IngressToken token)
    {
        var left = _pending.AddOrUpdate(token.ChannelId, 0, static (_, v) => Math.Max(0, v - 1));
        if (left == 0)
            limiter.Reset(token.ChannelId);
    }

    public void Reset(IChannelId channelId)
    {
        limiter.Reset(channelId);
        _pending.TryRemove(channelId, out _);
        _tokenCache.TryRemove(channelId, out _);
    }

    public IngressToken GetOrCreateToken(IChannelId channelId)
    {
        // Provide the cached token for the dispatcher. If no token exists,
        // allocate one and store it.
        return _tokenCache.GetOrAdd(channelId, static id => new IngressToken(id));
    }
}
