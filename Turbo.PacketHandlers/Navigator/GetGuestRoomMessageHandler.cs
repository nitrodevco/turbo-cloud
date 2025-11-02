using System.Threading;
using System.Threading.Tasks;
using Turbo.Contracts.Enums.Rooms.Object;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Navigator;
using Turbo.Primitives.Messages.Outgoing.Navigator;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Messages.Outgoing.Room.Layout;
using Turbo.Primitives.Messages.Outgoing.Room.Session;
using Turbo.Rooms.Abstractions;

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
        var roomGrain = await _roomService.GetRoomGrainAsync(message.RoomId).ConfigureAwait(false);
        var snapshot = await roomGrain.GetSnapshotAsync(ct).ConfigureAwait(false);

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

        await ctx
            .Session.SendComposerAsync(
                new OpenConnectionMessageComposer { RoomId = (int)snapshot.Id },
                ct
            )
            .ConfigureAwait(false);

        await ctx
            .Session.SendComposerAsync(
                new RoomReadyMessageComposer
                {
                    RoomType = snapshot.ModelId.ToString(), // need name from db
                    RoomId = (int)snapshot.Id,
                },
                ct
            )
            .ConfigureAwait(false);

        await ctx
            .Session.SendComposerAsync(
                new RoomEntryTileMessageComposer
                {
                    X = 0,
                    Y = 0,
                    Rotation = Rotation.North,
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
