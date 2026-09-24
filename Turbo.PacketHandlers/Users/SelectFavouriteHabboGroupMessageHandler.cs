using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Users;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Users;

public class SelectFavouriteHabboGroupMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<SelectFavouriteHabboGroupMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        SelectFavouriteHabboGroupMessage message,
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
            .SetFavouriteGuildAsync(message.GuildId, ct)
            .ConfigureAwait(false);
    }
}
