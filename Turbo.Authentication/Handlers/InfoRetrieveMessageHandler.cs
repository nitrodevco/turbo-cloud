using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Players;
using Turbo.Primitives.Messages.Incoming.Handshake;
using Turbo.Primitives.Messages.Outgoing.Handshake;

namespace Turbo.Authentication.Handlers;

public class InfoRetrieveMessageHandler(PlayerManager playerManager)
    : IMessageHandler<InfoRetrieveMessage>
{
    private readonly PlayerManager _playerManager = playerManager;

    public async ValueTask HandleAsync(
        InfoRetrieveMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        var playerId = ctx.Session.PlayerId;

        if (playerId <= 0)
        {
            return;
        }

        var player = await _playerManager.GetPlayerGrainAsync(playerId).ConfigureAwait(false);
        var snapshot = await player.GetSnapshotAsync(ct).ConfigureAwait(false);

        await ctx
            .Session.SendComposerAsync(new UserObjectMessage { Player = snapshot }, ct)
            .ConfigureAwait(false);
    }
}
