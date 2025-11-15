using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Players;
using Turbo.Primitives.Messages.Incoming.Handshake;
using Turbo.Primitives.Messages.Outgoing.Handshake;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;

namespace Turbo.PacketHandlers.Handshake;

public class InfoRetrieveMessageHandler(
    IPlayerService playerService,
    ISessionGateway sessionGateway
) : IMessageHandler<InfoRetrieveMessage>
{
    private readonly IPlayerService _playerService = playerService;
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

        var player = _playerService.GetPlayerGrain(playerId);
        var snapshot = await player.GetSummaryAsync(ct).ConfigureAwait(false);

        await ctx
            .Session.SendComposerAsync(new UserObjectMessage { Player = snapshot }, ct)
            .ConfigureAwait(false);
    }
}
