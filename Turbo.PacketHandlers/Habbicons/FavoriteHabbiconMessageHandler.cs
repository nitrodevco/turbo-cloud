using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Habbicons;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Habbicons;

/// <summary>An owned Habbicon added to the favourites (<c>HabbiconController.favoriteHabbicon</c>).</summary>
public class FavoriteHabbiconMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<FavoriteHabbiconMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        FavoriteHabbiconMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        await _grainFactory
            .GetPlayerHabbiconGrain(ctx.PlayerId)
            .SetFavouriteAsync(message.HabbiconId, true, ct)
            .ConfigureAwait(false);
    }
}
