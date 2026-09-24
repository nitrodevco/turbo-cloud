using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Users;
using Turbo.Primitives.Messages.Outgoing.Users;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Users;

public class GetHabboGroupDetailsMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<GetHabboGroupDetailsMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        GetHabboGroupDetailsMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || message.GuildId <= 0)
            return;

        var view = await _grainFactory
            .GetGuildGrain(message.GuildId)
            .GetViewAsync(ctx.PlayerId, ct)
            .ConfigureAwait(false);

        if (view is null)
            return;

        // The group grain holds none of these and must not call the grains that do, so they are
        // read here, side by side, the way a profile reads its badges.
        var roomTask = _grainFactory.GetRoomGrain(view.Guild.RoomId).GetSummaryAsync(ct);
        var ownerNameTask = _grainFactory
            .GetPlayerDirectoryGrain()
            .GetPlayerNameAsync(view.Guild.OwnerId, ct);
        var favouriteTask = _grainFactory
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
                    OpenDetails = message.OpenDetails,
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
