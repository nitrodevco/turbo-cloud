using System.Collections.Generic;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Furniture.Wall;
using Turbo.Primitives.Rooms.Snapshots.Wired;
using Turbo.Primitives.Rooms.Wired;

namespace Turbo.Rooms.Wired;

public sealed class WiredExecutionContext : WiredContext, IWiredExecutionContext
{
    public List<WiredUserMovementSnapshot> UserMoves { get; } = [];
    public List<WiredFloorItemMovementSnapshot> FloorItemMoves { get; } = [];
    public List<WiredWallItemMovementSnapshot> WallItemMoves { get; } = [];
    public List<WiredUserDirectionSnapshot> UserDirections { get; } = [];

    public void AddFloorItemMovement(
        IRoomFloorItem floorItem,
        int tileIdx,
        Altitude? z,
        Rotation? rot
    )
    {
        if (floorItem is null)
            return;

        try
        {
            var (sourceX, sourceY, sourceZ) = (floorItem.X, floorItem.Y, floorItem.Z);

            var finalZ = z ?? floorItem.Z;
            var finalRot = rot ?? floorItem.Rotation;

            if (!Room.MapModule.MoveFloorItem(floorItem, tileIdx, z, rot))
                return;

            FloorItemMoves.Add(
                new()
                {
                    ObjectId = floorItem.ObjectId,
                    SourceX = sourceX,
                    SourceY = sourceY,
                    SourceZ = sourceZ,
                    TargetX = floorItem.X,
                    TargetY = floorItem.Y,
                    TargetZ = floorItem.Z,
                    Rotation = floorItem.Rotation,
                    AnimationTime =
                        Policy.AnimationMode == WiredAnimationModeType.Instant
                            ? 0
                            : Policy.AnimationTimeMs,
                }
            );
        }
        catch { }
    }

    public void AddWallItemMovement(
        IRoomWallItem wallItem,
        int x,
        int y,
        Altitude z,
        Rotation rot,
        int wallOffset
    )
    {
        if (wallItem is null)
            return;

        try
        {
            var (sourceX, sourceY, sourceZ, sourceRot, sourceOffset) = (
                wallItem.X,
                wallItem.Y,
                wallItem.Z,
                wallItem.Rotation,
                wallItem.WallOffset
            );

            if (!Room.MapModule.MoveWallItem(wallItem, x, y, z, rot, wallOffset))
                return;

            WallItemMoves.Add(
                new()
                {
                    ObjectId = wallItem.ObjectId,
                    IsDirectionRight = wallItem.Rotation != Rotation.South,
                    SourceX = sourceX,
                    SourceY = sourceY,
                    SourceWallOffset = sourceOffset,
                    SourceZ = (int)sourceZ,
                    TargetX = wallItem.X,
                    TargetY = wallItem.Y,
                    TargetWallOffset = wallItem.WallOffset,
                    TargetZ = (int)wallItem.Z,
                }
            );
        }
        catch { }
    }

    public ActionContext AsActionContext() =>
        new()
        {
            Origin = ActionOrigin.Wired,
            SessionKey = SessionKey.Invalid,
            PlayerId = PlayerId.Invalid,
            RoomId = Room._state.RoomId,
        };

    public Task SendComposerToRoomAsync(IComposer composer) =>
        Room.SendComposerToRoomAsync(composer);
}
