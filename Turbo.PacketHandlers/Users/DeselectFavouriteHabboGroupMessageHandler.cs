using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Users;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Users;

public class DeselectFavouriteHabboGroupMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<DeselectFavouriteHabboGroupMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        DeselectFavouriteHabboGroupMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        // The player's grain owns the choice; it tells the room they are standing in, because
        // the badge on their avatar is what everybody else sees change.
        await _grainFactory
            .GetPlayerGuildGrain(ctx.PlayerId)
            .SetFavouriteGuildAsync(null, ct)
            .ConfigureAwait(false);
    }
}
