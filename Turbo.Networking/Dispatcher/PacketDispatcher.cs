namespace Turbo.Networking.Dispatcher;

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using DotNetty.Transport.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Turbo.Core.Configuration;
using Turbo.Core.Networking;
using Turbo.Core.Networking.Dispatcher;
using Turbo.Core.Networking.Session;
using Turbo.Core.Packets;
using Turbo.Core.Packets.Messages;
using Turbo.Core.Packets.Revisions;

public class PacketDispatcher(
    ISessionManager sessionManager,
    IRevisionManager revisionManager,
    IPacketMessageHub messageHub,
    IRateLimiter rateLimiter,
    IPacketQueue queue,
    IBackpressureManager backpressure,
    IEmulatorConfig config,
    ILogger<PacketDispatcher> logger) : BackgroundService, IPacketDispatcher
{
    private readonly IPacketQueue _queue = queue;
    private readonly IRateLimiter _rateLimiter = rateLimiter;
    private readonly IBackpressureManager _backpressure = backpressure;
    private readonly ISessionManager _sessionManager = sessionManager;
    private readonly IRevisionManager _revisionManager = revisionManager;
    private readonly IPacketMessageHub _messageHub = messageHub;
    private readonly IEmulatorConfig _config = config;
    private readonly ILogger<PacketDispatcher> _logger = logger;
    private readonly ConcurrentDictionary<IChannelId, Task> _sequencers = new();
    private readonly ConcurrentDictionary<IChannelId, int> _pendingCount = new();

    public bool TryEnqueue(IChannelId channelId, IClientPacket packet, out PacketRejectType rejectType)
    {
        rejectType = PacketRejectType.None;

        // 1) Rate limit
        if (!_rateLimiter.TryAcquire(channelId, out var exceededLimit))
        {
            rejectType = PacketRejectType.RateLimited;
            if (exceededLimit)
            {
                _ = _sessionManager.KickSessionAsync(channelId, SessionKickType.RateLimited);
            }

            return false;
        }

        // 2) Per‑session pending queue
        var pending = _pendingCount.AddOrUpdate(channelId, 1, (_, v) => v + 1);
        if (pending > _config.Network.DispatcherOptions.MaxPendingPerSession)
        {
            rejectType = PacketRejectType.Busy;
            _pendingCount.AddOrUpdate(channelId, 0, (_, v) => v - 1);
            return false;
        }

        // 3) Global queue
        if (!_queue.TryEnqueue(new PacketEnvelope(channelId, packet)))
        {
            rejectType = PacketRejectType.ServerBusy;
            _pendingCount.AddOrUpdate(channelId, 0, (_, v) => v - 1);
            return false;
        }

        // 4) Backpressure hint
        _backpressure.UpdateDepth(_queue.ApproxDepth, _sessionManager);
        return true;
    }

    protected override Task ExecuteAsync(CancellationToken ct)
    {
        return Task.WhenAll(Enumerable.Range(0, _config.Network.DispatcherOptions.Workers)
            .Select(_ => Worker(ct)));
    }

    private async Task Worker(CancellationToken ct)
    {
        await foreach (var envelope in _queue.ReadAllAsync(ct))
        {
            var next = _sequencers.AddOrUpdate(
                envelope.ChannelId,
                _ => ProcessOne(envelope, ct),
                (_, tail) => tail.ContinueWith(_ => ProcessOne(envelope, ct), ct,
                    TaskContinuationOptions.None, TaskScheduler.Default).Unwrap());

            _ = next.ContinueWith(
                t =>
            {
                if (t.IsFaulted)
                {
                    _logger.LogError(t.Exception, "Processing chain fault sid={Sid}", envelope.ChannelId);
                }
            }, TaskScheduler.Default);
        }
    }

    private async Task ProcessOne(PacketEnvelope envelope, CancellationToken ct)
    {
        try
        {
            if (!_sessionManager.TryGetSession(envelope.ChannelId, out var session))
            {
                // session gone; drop
                return;
            }

            try
            {
                var revision = _revisionManager.GetRevision(session.RevisionId);

                if (revision is not null)
                {
                    if (revision.Parsers.TryGetValue(envelope.Msg.Header, out var parser))
                    {
                        await parser.HandleAsync(session, envelope.Msg, _messageHub, ct);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Handler error {Header} sid={Sid}", envelope.Msg.Header, envelope.ChannelId);

                // await session.SendAsync(Out.Error("server-error"));
            }
        }
        finally
        {
            envelope.Msg.Content.Release();
            var left = _pendingCount.AddOrUpdate(envelope.ChannelId, 0, (_, v) => Math.Max(0, v - 1));
            if (left == 0)
            {
                _rateLimiter.Reset(envelope.ChannelId);
            }
        }
    }

    public void ResetForChannelId(IChannelId channelId)
    {
        _rateLimiter.Reset(channelId);
        _pendingCount.TryRemove(channelId, out _);
        _sequencers.TryRemove(channelId, out _);
    }
}
