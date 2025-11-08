using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Turbo.Furniture.Configuration;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Room.Engine;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Messages.Outgoing.Room.Layout;
using Turbo.Rooms.Abstractions;

namespace Turbo.PacketHandlers.Room;

public class GetHeightMapMessageHandler(
    IRoomService roomService,
    IOptions<FurnitureConfig> furnitureConfig
) : IMessageHandler<GetHeightMapMessage>
{
    private readonly IRoomService _roomService = roomService;
    private readonly FurnitureConfig _furnitureConfig = furnitureConfig.Value;

    public async ValueTask HandleAsync(
        GetHeightMapMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        var activeRoomId = ctx.Session.ActiveRoomId;

        if (activeRoomId is -1)
            return;

        var roomGrain = await _roomService.GetRoomGrainAsync(activeRoomId).ConfigureAwait(false);
        var mapSnapshot = await roomGrain.GetMapSnapshotAsync().ConfigureAwait(false);
        var floorItemSnapshots = await roomGrain.GetFloorItemSnapshotsAsync().ConfigureAwait(false);

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
        await ctx
            .Session.SendComposerAsync(
                new ObjectsMessageComposer { OwnerNames = [], FloorItems = floorItemSnapshots },
                ct
            )
            .ConfigureAwait(false);
    }
}
