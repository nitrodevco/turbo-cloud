using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Habbicons;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Habbicons;

/// <summary>A Habbicon taken out of the favourites (<c>HabbiconController.unfavoriteHabbicon</c>).</summary>
public class UnfavoriteHabbiconMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<UnfavoriteHabbiconMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        UnfavoriteHabbiconMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await _grainFactory
            .GetPlayerHabbiconGrain(ctx.PlayerId)
            .SetFavouriteAsync(message.HabbiconId, false, ct)
            .ConfigureAwait(false);
    }
}
