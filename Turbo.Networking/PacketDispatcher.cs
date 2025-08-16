using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using DotNetty.Transport.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Turbo.Core.Configuration;
using Turbo.Core.Networking;
using Turbo.Core.Networking.Session;
using Turbo.Core.Packets;
using Turbo.Core.Packets.Messages;
using Turbo.Core.Packets.Revisions;
using Turbo.Utilities;

namespace Turbo.Networking;

public class PacketDispatcher : BackgroundService, IPacketDispatcher
{
    private readonly Channel<PacketEnvelope> _queue;
    private readonly ConcurrentDictionary<IChannelId, Task> _sequencers = new();
    private readonly ConcurrentDictionary<IChannelId, int> _pendingCount = new();
    private readonly ConcurrentDictionary<IChannelId, int> _rateViolations = new();

    private readonly ISessionManager _sessionManager;
    private readonly IRevisionManager _revisionManager;
    private readonly IPacketMessageHub _messageHub;
    private readonly TokenBucket<IChannelId> _rate;
    private readonly IDispatcherOptions _options;
    private readonly ILogger<PacketDispatcher> _logger;

    public PacketDispatcher(
        ISessionManager sessionManager,
        IRevisionManager revisionManager,
        IPacketMessageHub messageHub,
        IDispatcherOptions options,
        ILogger<PacketDispatcher> logger)
    {
        _sessionManager = sessionManager;
        _revisionManager = revisionManager;
        _messageHub = messageHub;
        _options = options;
        _logger = logger;

        _queue = Channel.CreateBounded<PacketEnvelope>(new BoundedChannelOptions(_options.GlobalQueueCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = false
        });

        _rate = new TokenBucket<IChannelId>(_options.RateCapacity, _options.RateRefillPerSec);
    }

    public bool TryEnqueue(IChannelId channelId, IClientPacket packet, out PacketRejectType rejectType)
    {
        rejectType = PacketRejectType.None;

        if (!_rate.TryTake(channelId))
        {
            var strikes = _rateViolations.AddOrUpdate(channelId, 1, (_, v) => v + 1);

            rejectType = PacketRejectType.RateLimited;

            _logger.LogWarning("Rate limited sid={Sid} strikes={Strikes}", channelId, strikes);

            if (_sessionManager.TryGetSession(channelId, out var session))
            {
                session.PauseReads();
            }

            if (strikes >= _options.RateViolationsBeforeKick)
            {
                // kick the client; your code to close the channel here
                _ = KickAsync(channelId, "rate-limit");
            }

            // Caller should release the buffer since we didn't accept it
            return false;
        }

        // 2) soft cap per-session pending (protect ordering chains)
        var pending = _pendingCount.AddOrUpdate(channelId, 1, (_, v) => v + 1);

        if (pending > _options.MaxPendingPerSession)
        {
            rejectType = PacketRejectType.Busy;

            _logger.LogWarning("Session overloaded sid={Sid} pending={Pending}", channelId, pending);

            if (_sessionManager.TryGetSession(channelId, out var session)) session.PauseReads();

            _pendingCount.AddOrUpdate(channelId, 0, (_, v) => v - 1);

            return false;
        }

        var ok = _queue.Writer.TryWrite(new PacketEnvelope(channelId, packet));

        if (!ok)
        {
            rejectType = PacketRejectType.ServerBusy;

            _pendingCount.AddOrUpdate(channelId, 0, (_, v) => v - 1);

            return false;
        }

        // Global backpressure hint (optional)
        var approxDepth = _queue.Reader.Count; // rough; O(1) approximation in .NET 9; else track yourself

        if (approxDepth > _options.PauseReadsThresholdGlobal)
            _sessionManager.PauseReadsOnAll();
        else if (approxDepth < _options.ResumeReadsThresholdGlobal)
            _sessionManager.ResumeReadsOnAll();

        return true;
    }

    protected override Task ExecuteAsync(CancellationToken ct)
    {
        var workers = Enumerable.Range(0, _options.Workers).Select(_ => Worker(ct)).ToArray();

        return Task.WhenAll(workers);
    }

    private async Task Worker(CancellationToken ct)
    {
        await foreach (var envelope in _queue.Reader.ReadAllAsync(ct))
        {
            // chain work behind the session's tail task to preserve order
            var next = _sequencers.AddOrUpdate(envelope.ChannelId,
                _ => ProcessOne(envelope, ct),
                (_, tail) => tail.ContinueWith(_ => ProcessOne(envelope, ct), ct,
                    TaskContinuationOptions.None, TaskScheduler.Default).Unwrap());

            _ = next.ContinueWith(t =>
            {
                if (t.IsFaulted) _logger.LogError(t.Exception, "Processing chain fault sid={Sid}", envelope.ChannelId);
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
                //await session.SendAsync(Out.Error("server-error"));
            }
        }
        finally
        {
            // Always release buffers
            envelope.Msg.Content.Release();

            // Decrement pending counter and maybe resume reads
            var left = _pendingCount.AddOrUpdate(envelope.ChannelId, 0, (_, v) => Math.Max(0, v - 1));

            if (left == 0)
            {
                _rateViolations.TryRemove(envelope.ChannelId, out _); // forgive past sins when caught up
                if (_sessionManager.TryGetSession(envelope.ChannelId, out var s)) s.ResumeReads();
            }
        }
    }

    private Task KickAsync(IChannelId channelId, string reason)
    {
        if (_sessionManager.TryGetSession(channelId, out var s))
        {
            _logger.LogWarning("Kicking sid={Sid} reason={Reason}", channelId, reason);
            try { s.Channel.CloseAsync(); } catch { /* ignore */ }
        }
        _rate.Reset(channelId);
        _pendingCount.TryRemove(channelId, out _);
        _sequencers.TryRemove(channelId, out _);
        _rateViolations.TryRemove(channelId, out _);
        return Task.CompletedTask;
    }
}
