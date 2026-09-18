using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.RoomSettings;
using Turbo.Primitives.Messages.Outgoing.Roomsettings;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.RoomSettings;

/// <summary>
/// The ban list tab of room settings.
/// </summary>
public class GetBannedUsersFromRoomMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<GetBannedUsersFromRoomMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        GetBannedUsersFromRoomMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || message.RoomId <= 0)
            return;

        var players = await _grainFactory
            .GetRoomGrain(message.RoomId)
            .GetBannedPlayersAsync(ctx.AsActionContext(), ct)
            .ConfigureAwait(false);

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
