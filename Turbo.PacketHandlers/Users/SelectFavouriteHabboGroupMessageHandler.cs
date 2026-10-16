using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Users;
using Turbo.Primitives.Messages.Outgoing.Users;
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
        var changed = await _grainFactory
            .GetPlayerGuildGrain(ctx.PlayerId)
            .SetFavouriteGuildAsync(message.GuildId, ct)
            .ConfigureAwait(false);

        if (!changed)
            return;

        // Habbo then tells the player their profile changed, which re-reads an open profile so
        // its group star moves (official client capture, 2026-10-13: ExtendedProfileChanged
        // after both Select- and DeselectFavouriteHabboGroup; HabboGroupsManager.onExtendedProfileChanged).
        // Through the player's presence, not the session: it follows the room's badge messages
        // (14:21, Select: HabboGroupBadges, FavoriteMembershipUpdate, then this; Deselect:
        // FavoriteMembershipUpdate, then this), which the change only told the room to send
        // (IPlayerPresenceGrain.HoldForRoomAsync).
        await _grainFactory
            .SendComposerToPlayerAsync(
                ctx.PlayerId,
                new ExtendedProfileChangedMessageComposer { UserId = ctx.PlayerId.Value },
                ct
            )
            .ConfigureAwait(false);
    }
}
