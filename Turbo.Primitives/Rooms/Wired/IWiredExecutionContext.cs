using System.Collections.Generic;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Furniture.Wall;
using Turbo.Primitives.Rooms.Snapshots.Wired;

namespace Turbo.Primitives.Rooms.Wired;

public interface IWiredExecutionContext : IWiredContext
{
    public List<WiredUserMovementSnapshot> UserMoves { get; }
    public List<WiredFloorItemMovementSnapshot> FloorItemMoves { get; }
    public List<WiredWallItemMovementSnapshot> WallItemMoves { get; }
    public List<WiredUserDirectionSnapshot> UserDirections { get; }

    public void AddFloorItemMovement(
        IRoomFloorItem floorItem,
        int tileIdx,
        Altitude? z = null,
        Rotation? rotation = null
    );
    public void AddWallItemMovement(
        IRoomWallItem wallItem,
        int x,
        int y,
        Altitude z,
        Rotation rot,
        int wallOffset
    );
    public ActionContext AsActionContext();
    public Task SendComposerToRoomAsync(IComposer composer);
}
