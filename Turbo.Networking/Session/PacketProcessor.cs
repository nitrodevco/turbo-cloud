using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Turbo.Contracts.Abstractions;
using Turbo.Messages;
using Turbo.Networking.Abstractions.Revisions;
using Turbo.Networking.Abstractions.Session;
using Turbo.Packets.Abstractions;

namespace Turbo.Networking.Session;

public class PacketProcessor(
    IRevisionManager revisionManager,
    MessageSystem messageSystem,
    ILoggerFactory loggerFactory
)
{
    private readonly IRevisionManager _revisionManager = revisionManager;
    private readonly MessageSystem _messageSystem = messageSystem;
    private readonly ILogger _logger = loggerFactory.CreateLogger(nameof(PacketProcessor));

    public async Task ProcessPacket(
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
                var message = parser.Parse(clientPacket);

                _logger.LogInformation(
                    "Processing {PacketType} for {SessionId}",
                    message.GetType().Name,
                    ctx.SessionID
                );

                await _messageSystem.PublishAsync(message, ctx, ct);
            }
            else
            {
                _logger.LogInformation(
                    "Invalid packet {PacketHeader} for {SessionId}",
                    clientPacket.Header,
                    ctx.SessionID
                );
            }
        }
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

                if (ctx.Rc4Engine is not null)
                    data = ctx.Rc4Engine.ProcessBytes(data);

                await ctx.SendAsync(data, ct).ConfigureAwait(false);
            }
        }

        await Task.CompletedTask;
    }
}
