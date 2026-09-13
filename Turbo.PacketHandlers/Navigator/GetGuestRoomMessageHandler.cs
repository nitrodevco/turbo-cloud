using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Navigator;
using Turbo.Primitives.Messages.Outgoing.Navigator;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.PacketHandlers.Navigator;

/// <summary>
/// Answers a room info request. With <c>enterRoom = false, roomForward = true</c> the client
/// picked the room in the navigator. <c>OpeningConnection</c> tells the client whether the server
/// is opening the room on its behalf; when false the client shows the doorbell or password prompt,
/// or sends OpenFlatConnection itself. With <c>enterRoom = true</c> the client is already inside
/// and only wants the info.
/// </summary>
public class GetGuestRoomMessageHandler(IRoomService roomService, IGrainFactory grainFactory)
    : IMessageHandler<GetGuestRoomMessage>
{
    private readonly IRoomService _roomService = roomService;
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        GetGuestRoomMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || message.RoomId <= 0)
            return;

        var roomGrain = _grainFactory.GetRoomGrain(message.RoomId);
        var snapshot = await roomGrain.GetSnapshotAsync().ConfigureAwait(false);
        var population = await roomGrain.GetRoomPopulationAsync().ConfigureAwait(false);
        var allInRoomMuted = await roomGrain.GetIsRoomMutedAsync().ConfigureAwait(false);

        var isNavigatorForward = !message.EnterRoom && message.RoomForward;
        var isOpening = false;

        if (isNavigatorForward)
        {
            var access = await roomGrain
                .CheckEntryAccessAsync(ctx.PlayerId, null, bypassDoor: false, ct)
                .ConfigureAwait(false);

            isOpening = access == RoomEntryAccessType.Allowed;
        }

        var staffPick = false;
        var groupMember = false;
        var canMute = false;

        await ctx.SendComposerAsync(
                new GetGuestRoomResultMessageComposer
                {
                    EnterRoom = message.EnterRoom,
                    RoomInfo = snapshot with { Population = population },
                    RoomForward = message.RoomForward,
                    StaffPick = staffPick,
                    IsGroupMember = groupMember,
                    AllInRoomMuted = allInRoomMuted,
                    CanMute = canMute,
                    OpeningConnection = isOpening,
                },
                ct
            )
            .ConfigureAwait(false);

        if (isOpening)
            await _roomService
                .OpenRoomForPlayerIdAsync(
                    ctx.AsActionContext(),
                    ctx.PlayerId,
                    message.RoomId,
                    RoomEntryType.Navigator,
                    ct
                )
                .ConfigureAwait(false);
    }
}
