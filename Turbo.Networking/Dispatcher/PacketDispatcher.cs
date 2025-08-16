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
using Turbo.Core.Networking.Behaviors;
using Turbo.Core.Networking.Dispatcher;
using Turbo.Core.Networking.Pipeline;
using Turbo.Core.Networking.Session;
using Turbo.Core.Packets;
using Turbo.Core.Packets.Messages;
using Turbo.Core.Packets.Revisions;
using Turbo.Networking.Behaviors;
using Turbo.Networking.Pipelines;

namespace Turbo.Networking.Dispatcher;

public class PacketDispatcher(
    ISessionManager sessionManager,
    IRevisionManager revisionManager,
    IPacketMessageHub messageHub,
    IPacketQueue queue,
    IIngressPipeline ingress,
    IEmulatorConfig config,
    ILogger<PacketDispatcher> logger
) : BackgroundService, IPacketDispatcher
{
    private readonly ISessionManager _sessionManager = sessionManager;
    private readonly IPacketQueue _queue = queue;
    private readonly IIngressPipeline _ingress = ingress;
    private readonly IEmulatorConfig _config = config;
    private readonly ILogger<PacketDispatcher> _logger = logger;

    private readonly IProcessingPipeline _processing = new ProcessingPipeline(
        [
            new SequencingBehavior(logger),
            new ExceptionHandlingBehavior(logger),
            new RevisionDispatchBehavior(revisionManager, messageHub, logger),
            new CleanupBehavior(ingress, logger),
        ]
    );

    public bool TryEnqueue(
        IChannelId channelId,
        IClientPacket packet,
        out PacketRejectType rejectType
    )
    {
        var env = new PacketEnvelope(channelId, packet);
        var ok = _ingress.TryAccept(env, out rejectType, out _);
        return ok;
    }

    protected override Task ExecuteAsync(CancellationToken ct) =>
        Task.WhenAll(
            Enumerable.Range(0, _config.Network.DispatcherOptions.Workers).Select(_ => Worker(ct))
        );

    private async Task Worker(CancellationToken ct)
    {
        await foreach (var env in _queue.ReadAllAsync(ct))
        {
            if (!_sessionManager.TryGetSession(env.ChannelId, out var session))
            {
                _logger.LogWarning("Dropping packet for unknown session sid={Sid}", env.ChannelId);
                continue;
            }

            // We need the token used when this env was accepted. Easiest: add it into the envelope at ingress time.
            // If you don't want to mutate PacketEnvelope, re-resolve it here:
            var token = new IngressToken(env.ChannelId);

            var ctx = new PacketContext(session, _logger, token);
            await _processing.ProcessAsync(ctx, env, ct);
        }
    }

    public void ResetForChannelId(IChannelId channelId) => _ingress.Reset(channelId);
}
