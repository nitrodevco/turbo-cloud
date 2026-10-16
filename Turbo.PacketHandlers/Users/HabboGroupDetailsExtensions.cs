using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Guilds;
using Turbo.Primitives.Messages.Outgoing.Users;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Users;

/// <summary>
/// A group's details as the player being handled sees them. Asked for by the client, and also
/// sent unasked after a join, which is what redraws the join button the details window disabled
/// when it was clicked (GroupDetailsCtrl.as:175).
/// </summary>
internal static class HabboGroupDetailsExtensions
{
    public static async Task SendHabboGroupDetailsAsync(
        this MessageContext ctx,
        IGrainFactory grainFactory,
        GuildId guildId,
        bool openDetails,
        CancellationToken ct
    )
    {
        var view = await grainFactory
            .GetGuildGrain(guildId)
            .GetViewAsync(ctx.PlayerId, ct)
            .ConfigureAwait(false);

        if (view is null)
            return;

        // The group grain holds none of these and must not call the grains that do, so they are
        // read here, side by side, the way a profile reads its badges.
        var roomTask = grainFactory.GetRoomGrain(view.Guild.RoomId).GetSummaryAsync(ct);
        var ownerNameTask = grainFactory
            .GetPlayerDirectoryGrain()
            .GetPlayerNameAsync(view.Guild.OwnerId, ct);
        var favouriteTask = grainFactory
            .GetPlayerGuildGrain(ctx.PlayerId)
            .GetFavouriteGuildIdAsync(ct);

        await Task.WhenAll(roomTask, ownerNameTask, favouriteTask).ConfigureAwait(false);

        var room = await roomTask.ConfigureAwait(false);
        var ownerName = await ownerNameTask.ConfigureAwait(false);
        var favouriteGuildId = await favouriteTask.ConfigureAwait(false);

        await ctx.SendComposerAsync(
                new HabboGroupDetailsMessageComposer
                {
                    View = view,
                    RoomName = room.Name,
                    OwnerName = ownerName,
                    IsFavourite = favouriteGuildId == view.Guild.GuildId,
                    OpenDetails = openDetails,
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
