using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Handshake;
using Turbo.Primitives.Messages.Outgoing.Handshake;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Handshake;

public class InfoRetrieveMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<InfoRetrieveMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        InfoRetrieveMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        var player = _grainFactory.GetPlayerGrain(ctx.PlayerId);
        var snapshot = await player.GetSummaryAsync(ct).ConfigureAwait(false);

        await ctx.SendComposerAsync(new UserObjectMessage { Player = snapshot }, ct)
            .ConfigureAwait(false);
    }
}
