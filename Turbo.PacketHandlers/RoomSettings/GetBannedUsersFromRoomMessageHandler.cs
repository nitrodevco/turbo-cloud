using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.RoomSettings;
using Turbo.Primitives.Messages.Outgoing.Roomsettings;
using Turbo.Primitives.Navigator;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.RoomSettings;

/// <summary>
/// The ban list tab of room settings.
/// </summary>
public class GetBannedUsersFromRoomMessageHandler(
    IGrainFactory grainFactory,
    INavigatorService navigatorService
) : IMessageHandler<GetBannedUsersFromRoomMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;
    private readonly INavigatorService _navigatorService = navigatorService;

    public async ValueTask HandleAsync(
        GetBannedUsersFromRoomMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || message.RoomId <= 0)
            return;

        // Asked before the room grain, whose activation would fail on a missing room.
        if (!await _navigatorService.RoomExistsAsync(message.RoomId, ct).ConfigureAwait(false))
            return;

        var players = await _grainFactory
            .GetRoomGrain(message.RoomId)
            .GetBannedPlayersAsync(ctx.AsActionContext(), ct)
            .ConfigureAwait(false);

        // Null when the player may not see the list, or for a repeat the room lets go
        // (RoomModerationModule). An empty list is sent, as Habbo does.
        if (players is null)
            return;

        await ctx.SendComposerAsync(
                new BannedUsersFromRoomEventMessageComposer
                {
                    RoomId = message.RoomId,
                    Players = players.Value,
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
