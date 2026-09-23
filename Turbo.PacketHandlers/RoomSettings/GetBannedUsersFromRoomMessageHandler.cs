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

        // The Flash client only creates its ban dictionary while iterating the entries it receives,
        // so a zero-entry reply leaves the dictionary null and its refresh handler immediately asks
        // again -- an endless request loop. Staying silent leaves the tab empty, which is correct.
        if (players is null or { IsEmpty: true })
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
