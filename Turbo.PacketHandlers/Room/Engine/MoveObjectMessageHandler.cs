using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Room.Engine;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Orleans.Grains;
using Turbo.Primitives.Orleans.Grains.Room;

namespace Turbo.PacketHandlers.Room.Engine;

public class MoveObjectMessageHandler(ISessionGateway sessionGateway, IGrainFactory grainFactory)
    : IMessageHandler<MoveObjectMessage>
{
    private readonly ISessionGateway _sessionGateway = sessionGateway;
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        MoveObjectMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        var playerId = _sessionGateway.GetPlayerId(ctx.Session.SessionKey);

        if (playerId <= 0)
            return;

        var playerPresence = _grainFactory.GetGrain<IPlayerPresenceGrain>(playerId);
        var activeRoom = await playerPresence.GetActiveRoomAsync().ConfigureAwait(false);

        if (activeRoom.RoomId <= 0)
            return;

        var roomMap = _grainFactory.GetGrain<IRoomGrain>(activeRoom.RoomId);

        await roomMap
            .MoveFloorItemAsync(message.ObjectId, message.X, message.Y, message.Rotation, ct)
            .ConfigureAwait(false);
    }
}

internal interface IRoomPresenceGrain { }
