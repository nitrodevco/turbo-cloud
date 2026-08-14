using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.FriendList;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Rooms;

namespace Turbo.PacketHandlers.FriendList;

public class VisitUserMessageHandler(IGrainFactory grainFactory, IRoomService roomService)
    : IMessageHandler<VisitUserMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;
    private readonly IRoomService _roomService = roomService;

    public async ValueTask HandleAsync(
        VisitUserMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        var playerDirectory = _grainFactory.GetPlayerDirectoryGrain();
        var targetId = await playerDirectory
            .GetPlayerIdAsync(message.PlayerName, ct)
            .ConfigureAwait(false);

        if (targetId is null)
            return;

        var targetPresence = _grainFactory.GetPlayerPresenceGrain(targetId.Value);
        var activeRoom = await targetPresence.GetActiveRoomAsync().ConfigureAwait(false);

        if (activeRoom.RoomId <= 0)
            return;

        await _roomService
            .OpenRoomForPlayerIdAsync(ctx.AsActionContext(), ctx.PlayerId, activeRoom.RoomId, ct)
            .ConfigureAwait(false);
    }
}
