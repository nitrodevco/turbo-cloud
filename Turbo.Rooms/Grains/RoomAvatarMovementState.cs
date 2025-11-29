using System.Collections.Generic;
using Turbo.Contracts.Enums.Rooms.Object;
using Turbo.Primitives.Orleans.Snapshots.Room;
using Turbo.Primitives.Rooms.Mapping;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Furniture.Wall;
using Turbo.Primitives.Rooms.Snapshots;

namespace Turbo.Rooms.Grains;

internal sealed class RoomAvatarMovementState
{
    public RoomObjectId ObjectId { get; init; } = RoomObjectId.Empty;

    public int X { get; set; }
    public int Y { get; set; }
    public double Z { get; set; }
    public Rotation Rotation { get; set; }
    public Rotation HeadRotation { get; set; }

    public int GoalTileId { get; set; } = -1;
    public int NextTileId { get; set; } = -1;
    public int PrevTileId { get; set; } = -1;
    public bool IsWalking { get; set; } = false;

    public readonly Queue<int> TilePath = new();

    public static RoomAvatarMovementState From(RoomObjectId objectId, int startX, int startY) =>
        new()
        {
            ObjectId = objectId,
            X = startX,
            Y = startY,
        };
}
