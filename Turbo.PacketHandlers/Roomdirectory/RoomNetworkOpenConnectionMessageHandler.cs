using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Roomdirectory;
using Turbo.Primitives.Messages.Outgoing.Room.Session;
using Turbo.Primitives.Navigator;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Snapshots;

namespace Turbo.PacketHandlers.Roomdirectory;

/// <summary>
/// The reception's room hopper: the client has opened a room session for the network and waits
/// for the room. The player is forwarded to one of the network's rooms; with none, the session is
/// closed again so the client goes back to the reception.
/// </summary>
public class RoomNetworkOpenConnectionMessageHandler(
    INavigatorService navigatorService,
    IGrainFactory grainFactory,
    ILogger<RoomNetworkOpenConnectionMessageHandler> logger
) : IMessageHandler<RoomNetworkOpenConnectionMessage>
{
    private readonly INavigatorService _navigatorService = navigatorService;
    private readonly IGrainFactory _grainFactory = grainFactory;
    private readonly ILogger<RoomNetworkOpenConnectionMessageHandler> _logger = logger;

    public async ValueTask HandleAsync(
        RoomNetworkOpenConnectionMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        var roomId = await _navigatorService
            .GetRoomNetworkRoomAsync(message.NetworkId, message.HomeRoomId, ct)
            .ConfigureAwait(false);

        if (roomId is null)
        {
            _logger.LogWarning(
                "Player {PlayerId} asked for room network {NetworkId}, which has no rooms",
                ctx.PlayerId,
                message.NetworkId
            );

            await ctx.SendComposerAsync(new CloseConnectionMessageComposer(), ct)
                .ConfigureAwait(false);

            return;
        }

        await _grainFactory
            .ForwardPlayerToRoomAsync(
                ctx.PlayerId,
                roomId.Value,
                new RoomEntrySnapshot { Method = RoomEntryMethodType.RoomNetwork, TeleportId = 0 },
                ct
            )
            .ConfigureAwait(false);
    }
}
