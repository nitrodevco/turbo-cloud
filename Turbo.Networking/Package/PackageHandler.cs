using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SuperSocket.Server.Abstractions;
using SuperSocket.Server.Abstractions.Session;
using Turbo.Messages;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Networking.Revisions;
using Turbo.Primitives.Packets;

namespace Turbo.Networking.Package;

public sealed class PackageHandler(
    IRevisionManager revisionManager,
    MessageSystem messageSystem,
    ILogger<PackageHandler> logger
) : IPackageHandler<IClientPacket>
{
    private readonly IRevisionManager _revisionManager = revisionManager;
    private readonly MessageSystem _messageSystem = messageSystem;
    private readonly ILogger<PackageHandler> _logger = logger;

    public async ValueTask Handle(
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

                _logger.LogDebug("Incoming {Message}", message);

                await _messageSystem
                    .PublishAsync(message, ctx, CancellationToken.None)
                    .ConfigureAwait(false);
            }
            else
            {
                _logger.LogWarning(
                    "Incoming Unknown {Header} for {SessionKey}",
                    packet.Header,
                    ctx.SessionKey
                );
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to process packet {Packet} for session {SessionKey}",
                packet.Header,
                ctx.SessionKey
            );
        }
    }
}
