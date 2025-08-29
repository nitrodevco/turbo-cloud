using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Turbo.Core.Networking.Protocol;
using Turbo.Core.Networking.Session;
using Turbo.Core.Packets;
using Turbo.Core.Packets.Messages;
using Turbo.Core.Packets.Revisions;
using Turbo.Packets.Incoming;

namespace Turbo.Networking.Session;

public class PacketProcessor(
    IRevisionManager revisionManager,
    IPacketMessageHub packetMessageHub,
    ILogger<PacketProcessor> logger
) : IPacketProcessor
{
    private readonly IRevisionManager _revisionManager = revisionManager;
    private readonly IPacketMessageHub _packetMessageHub = packetMessageHub;
    private readonly ILogger<PacketProcessor> _logger = logger;

    public async Task ProcessClientPacket(
        ISessionContext ctx,
        IClientPacket clientPacket,
        CancellationToken ct
    )
    {
        var revision = _revisionManager.GetRevision(ctx.RevisionId);

        if (revision is null)
        {
            _logger.LogWarning("No revision set for session {SessionId}", ctx.SessionID);
        }
        else
        {
            if (revision.Parsers.TryGetValue(clientPacket.Header, out var parser))
            {
                await parser
                    .HandleAsync(ctx, clientPacket, _packetMessageHub, ct)
                    .ConfigureAwait(false);

                _logger.LogDebug(
                    "Processed packet {PacketHeader} for session {SessionId}",
                    clientPacket.Header,
                    ctx.SessionID
                );
            }
        }

        await Task.CompletedTask;
    }

    public async Task ProcessComposer(ISessionContext ctx, IComposer composer, CancellationToken ct)
    {
        var revision = _revisionManager.GetRevision(ctx.RevisionId);

        if (revision is null)
        {
            _logger.LogWarning("No revision set for session {SessionId}", ctx.SessionID);
        }
        else
        {
            if (revision.Serializers.TryGetValue(composer.GetType(), out var serializer))
            {
                var payload = serializer.Serialize(composer).ToArray();
                var data = new byte[payload.Length + 4];

                BinaryPrimitives.WriteInt32BigEndian(data.AsSpan(0, 4), payload.Length);
                payload.CopyTo(data.AsSpan(4));

                if (ctx.Rc4Service is not null)
                    data = ctx.Rc4Service.ProcessBytes(data);

                await ctx.SendAsync(data, ct);
            }
        }

        await Task.CompletedTask;
    }
}
