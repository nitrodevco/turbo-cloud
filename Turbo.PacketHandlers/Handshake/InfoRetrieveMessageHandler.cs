using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Players;
using Turbo.Primitives.Messages.Incoming.Handshake;
using Turbo.Primitives.Messages.Outgoing.Handshake;
using Turbo.Primitives.Networking;

namespace Turbo.PacketHandlers.Handshake;

public class InfoRetrieveMessageHandler(PlayerManager playerManager, ISessionGateway sessionGateway)
    : IMessageHandler<InfoRetrieveMessage>
{
    private readonly PlayerManager _playerManager = playerManager;
    private readonly ISessionGateway _sessionGateway = sessionGateway;

    public async ValueTask HandleAsync(
        InfoRetrieveMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        var playerId = _sessionGateway.GetPlayerId(ctx.Session.SessionKey);

        if (playerId <= 0)
            return;

        var player = await _playerManager.GetPlayerGrainAsync(playerId).ConfigureAwait(false);
        var snapshot = await player.GetSnapshotAsync(ct).ConfigureAwait(false);

        await ctx
            .Session.SendComposerAsync(new UserObjectMessage { Player = snapshot }, ct)
            .ConfigureAwait(false);
    }
}
