using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Turbo.Contracts.Abstractions;
using Turbo.Messages;
using Turbo.Networking.Abstractions.Revisions;
using Turbo.Networking.Abstractions.Session;
using Turbo.Packets.Abstractions;

namespace Turbo.Networking.Session;

public sealed class PacketProcessor(
    IRevisionManager revisionManager,
    MessageSystem messageSystem,
    ILogger<PacketProcessor> logger,
    IServiceProvider sp
)
{
    private readonly IRevisionManager _revisionManager = revisionManager;
    private readonly MessageSystem _messageSystem = messageSystem;
    private readonly ILogger<PacketProcessor> _logger = logger;
    private readonly IServiceProvider _sp = sp;

    public async Task ProcessPacketAsync(
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
                    "Processing {Header} {PacketType} for {SessionId}",
                    clientPacket.Header,
                    message.GetType().Name,
                    ctx.SessionID
                );

                await _messageSystem.PublishAsync(message, ctx, ct).ConfigureAwait(false);
            }
            else
            {
                _logger.LogInformation(
                    "Invalid packet {Header} for {SessionId}",
                    clientPacket.Header,
                    ctx.SessionID
                );
            }
        }
    }

    public async Task ProcessComposerAsync(
        ISessionContext ctx,
        IComposer composer,
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
            if (revision.Serializers.TryGetValue(composer.GetType(), out var serializer))
            {
                var payload = serializer.Serialize(composer).ToArray();

                if (ctx.CryptoOut is not null)
                    payload = ctx.CryptoOut.Process(payload);

                await ctx.SendAsync(payload, ct).ConfigureAwait(false);

                _logger.LogInformation(
                    "Sent {Header} {PacketType} for {SessionId}",
                    serializer.Header,
                    composer.GetType().Name,
                    ctx.SessionID
                );
            }
        }

        await Task.CompletedTask.ConfigureAwait(false);
    }
}
