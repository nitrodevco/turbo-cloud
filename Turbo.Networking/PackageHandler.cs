using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SuperSocket.Server.Abstractions;
using SuperSocket.Server.Abstractions.Session;
using Turbo.Messages;
using Turbo.Networking.Abstractions.Revisions;
using Turbo.Networking.Abstractions.Session;
using Turbo.Packets.Abstractions;

namespace Turbo.Networking;

public sealed class PackageHandler(
    IRevisionManager revisionManager,
    MessageSystem messageSystem,
    ILogger<PackageHandler> logger
) : IPackageHandler<IClientPacket>
{
    private readonly IRevisionManager _revisionManager = revisionManager;
    private readonly MessageSystem _messageSystem = messageSystem;
    private readonly ILogger<PackageHandler> _logger = logger;

    public ValueTask Handle(
        IAppSession session,
        IClientPacket packet,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(packet);

        var ctx = (ISessionContext)session;

        try
        {
            var revision =
                _revisionManager.GetRevision(ctx.RevisionId)
                ?? throw new ArgumentNullException("No revision set");

            if (revision.Parsers.TryGetValue(packet.Header, out var parser))
            {
                var message = parser.Parse(packet);

                _logger.LogInformation(
                    "Incoming {Message} for {SessionId}",
                    message,
                    ctx.SessionID
                );

                _ = _messageSystem
                    .PublishAsync(message, ctx, CancellationToken.None)
                    .ConfigureAwait(false);
            }
            else
            {
                _logger.LogInformation(
                    "Incoming Unknown {Header} for {SessionId}",
                    packet.Header,
                    ctx.SessionID
                );
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to process packet {Packet} for session {SessionId}",
                packet.Header,
                session.SessionID
            );
        }

        return ValueTask.CompletedTask;
    }
}
