using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Handshake;
using Turbo.Primitives.Messages.Outgoing.Handshake;
using Turbo.Primitives.Players;

namespace Turbo.PacketHandlers.Handshake;

public class InfoRetrieveMessageHandler(IPlayerService playerService)
    : IMessageHandler<InfoRetrieveMessage>
{
    private readonly IPlayerService _playerService = playerService;

    public async ValueTask HandleAsync(
        InfoRetrieveMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        var player = _playerService.GetPlayerGrain(ctx.PlayerId);
        var snapshot = await player.GetSummaryAsync(ct).ConfigureAwait(false);

        await ctx.SendComposerAsync(new UserObjectMessage { Player = snapshot }, ct)
            .ConfigureAwait(false);
    }
}
