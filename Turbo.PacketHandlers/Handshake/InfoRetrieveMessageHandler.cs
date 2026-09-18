using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Players.Configuration;
using Turbo.Primitives.Messages.Incoming.Handshake;
using Turbo.Primitives.Messages.Outgoing.Handshake;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Handshake;

public class InfoRetrieveMessageHandler(
    IGrainFactory grainFactory,
    IOptions<PlayerConfig> playerConfig
) : IMessageHandler<InfoRetrieveMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;
    private readonly PlayerConfig _playerConfig = playerConfig.Value;

    public async ValueTask HandleAsync(
        InfoRetrieveMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        var player = _grainFactory.GetPlayerGrain(ctx.PlayerId);
        var snapshot = await player.GetSummaryAsync(ct).ConfigureAwait(false);

        await ctx.SendComposerAsync(
                new UserObjectMessage
                {
                    Player = snapshot,
                    MaxRespectPerDay = _playerConfig.MaxRespectPerDay,
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
