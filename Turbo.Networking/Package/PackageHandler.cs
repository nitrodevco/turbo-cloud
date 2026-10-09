using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SuperSocket.Server.Abstractions;
using SuperSocket.Server.Abstractions.Session;
using Turbo.Messages;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Networking.Extensions;
using Turbo.Primitives.Networking.Revisions;
using Turbo.Primitives.Packets;

namespace Turbo.Networking.Package;

public sealed class PackageHandler(
    IRevisionManager revisionManager,
    IExtensionPacketRegistry extensions,
    MessageSystem messageSystem,
    ILogger<PackageHandler> logger
) : IPackageHandler<IClientPacket>
{
    private readonly IRevisionManager _revisionManager = revisionManager;
    private readonly IExtensionPacketRegistry _extensions = extensions;
    private readonly MessageSystem _messageSystem = messageSystem;
    private readonly ILogger<PackageHandler> _logger = logger;

    public async ValueTask Handle(IAppSession session, IClientPacket packet, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(packet);

        // Marking the session as heard from is done as the packet is framed (TcpFilter,
        // ReceiveMarkingFilter), not here, where it would wait behind every handler before it.
        var ctx = (ISessionContext)session;

        try
        {
            var revision =
                _revisionManager.GetRevision(ctx.RevisionId)
                ?? throw new ArgumentNullException("No revision set");

            // Core first; the plugin overlay is only asked about a header the revision lacks.
            if (
                revision.Parsers.TryGetValue(packet.Header, out var parser)
                || _extensions.TryGetParser(packet.Header, out parser!)
            )
            {
                var message = parser.Parse(packet);

                if (_logger.IsEnabled(LogLevel.Debug))
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
