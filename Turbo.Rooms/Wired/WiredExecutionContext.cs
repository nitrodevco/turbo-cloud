using System.Collections.Generic;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Snapshots.Wired;
using Turbo.Primitives.Rooms.Wired;

namespace Turbo.Rooms.Wired;

public sealed class WiredExecutionContext : WiredContext, IWiredExecutionContext
{
    public List<WiredUserMovementSnapshot> UserMoves { get; } = [];
    public List<WiredFloorItemMovementSnapshot> FloorItemMoves { get; } = [];
    public List<WiredWallItemMovementSnapshot> WallItemMoves { get; } = [];
    public List<WiredUserDirectionSnapshot> UserDirections { get; } = [];

    public void AddFloorItemMovement(IRoomFloorItem floorItem, int tileIdx, Rotation rotation)
    {
        if (floorItem is null)
            return;

        try
        {
            var (sourceX, sourceY, sourceZ) = (floorItem.X, floorItem.Y, floorItem.Z);

            if (!Room.MapModule.MoveFloorItem(floorItem, tileIdx, rotation))
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
                    Rotation = rotation,
                    AnimationTime =
                        Policy.AnimationMode == WiredAnimationModeType.Instant
                            ? 0
                            : Policy.AnimationTimeMs,
                }
            );
        }
        catch { }
    }

    public ActionContext AsActionContext() =>
        new() { Origin = ActionOrigin.Wired, RoomId = Room._state.RoomId };

    public Task SendComposerToRoomAsync(IComposer composer) =>
        Room.SendComposerToRoomAsync(composer);
}
