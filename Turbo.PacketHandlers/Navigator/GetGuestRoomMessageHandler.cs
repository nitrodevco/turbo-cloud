using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Turbo.Contracts.Enums.Rooms;
using Turbo.Contracts.Enums.Rooms.Object;
using Turbo.Furniture.Configuration;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Navigator;
using Turbo.Primitives.Messages.Outgoing.Navigator;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Messages.Outgoing.Room.Layout;
using Turbo.Primitives.Messages.Outgoing.Room.Session;
using Turbo.Rooms.Abstractions;

namespace Turbo.PacketHandlers.Navigator;

public class GetGuestRoomMessageHandler(
    IRoomService roomService,
    IOptions<FurnitureConfig> furnitureConfig
) : IMessageHandler<GetGuestRoomMessage>
{
    private readonly IRoomService _roomService = roomService;
    private readonly FurnitureConfig _furnitureConfig = furnitureConfig.Value;

    public async ValueTask HandleAsync(
        GetGuestRoomMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        var roomGrain = await _roomService.GetRoomGrainAsync(message.RoomId).ConfigureAwait(false);
        var snapshot = await roomGrain.GetSnapshotAsync().ConfigureAwait(false);
        var mapSnapshot = await roomGrain.GetMapSnapshotAsync().ConfigureAwait(false);

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
                    RoomType = mapSnapshot.ModelName,
                    RoomId = (int)snapshot.Id,
                },
                ct
            )
            .ConfigureAwait(false);

        await ctx
            .Session.SendComposerAsync(
                new RoomEntryTileMessageComposer
                {
                    X = mapSnapshot.DoorX,
                    Y = mapSnapshot.DoorY,
                    Rotation = mapSnapshot.DoorRotation,
                },
                ct
            )
            .ConfigureAwait(false);

        await ctx
            .Session.SendComposerAsync(
                new HeightMapMessageComposer
                {
                    Width = mapSnapshot.Width,
                    Size = mapSnapshot.Size,
                    Heights = mapSnapshot.TileRelativeHeights,
                },
                ct
            )
            .ConfigureAwait(false);

        await ctx
            .Session.SendComposerAsync(
                new FloorHeightMapMessageComposer
                {
                    ScaleType = _furnitureConfig.DefaultRoomScale,
                    FixedWallsHeight = _furnitureConfig.DefaultWallHeight,
                    ModelData = mapSnapshot.ModelData,
                    AreaHideData = [],
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
