using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Navigator;
using Turbo.Primitives.Messages.Outgoing.Navigator;
using Turbo.Primitives.Rooms;

namespace Turbo.PacketHandlers.Navigator;

public class GetGuestRoomMessageHandler(IRoomService roomService)
    : IMessageHandler<GetGuestRoomMessage>
{
    private readonly IRoomService _roomService = roomService;

    public async ValueTask HandleAsync(
        GetGuestRoomMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        var roomGrain = _roomService.GetRoomGrain(message.RoomId);
        var snapshot = await roomGrain.GetSnapshotAsync().ConfigureAwait(false);

        var staffPick = false;
        var groupMember = false;
        var allInRoomMuted = false;
        var canMute = false;

        await ctx
            .Session.SendComposerAsync(
                new GetGuestRoomResultMessageComposer
                {
                    EnterRoom = message.EnterRoom,
                    RoomSnapshot = snapshot,
                    RoomForward = message.RoomForward,
                    StaffPick = staffPick,
                    IsGroupMember = groupMember,
                    AllInRoomMuted = allInRoomMuted,
                    CanMute = canMute,
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
