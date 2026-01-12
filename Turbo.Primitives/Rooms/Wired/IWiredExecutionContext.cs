using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic.Furniture;
using Turbo.Primitives.Rooms.Snapshots.Wired;

namespace Turbo.Primitives.Rooms.Wired;

public interface IWiredExecutionContext
{
    public Dictionary<string, object?> Variables { get; }
    public IWiredPolicy Policy { get; }
    public IWiredSelectionSet Selected { get; }
    public IWiredSelectionSet SelectorPool { get; }

    public List<WiredUserMovementSnapshot> UserMoves { get; }
    public List<WiredFloorItemMovementSnapshot> FloorItemMoves { get; }
    public List<WiredWallItemMovementSnapshot> WallItemMoves { get; }
    public List<WiredUserDirectionSnapshot> UserDirections { get; }

    public Task<IWiredSelectionSet> GetWiredSelectionSetAsync(
        IFurnitureWiredLogic wired,
        CancellationToken ct
    );
    public Task<IWiredSelectionSet> GetEffectiveSelectionAsync(
        IFurnitureWiredLogic wired,
        CancellationToken ct
    );
    public void AddFloorItemMovement(IRoomFloorItem floorItem, int tileIdx, Rotation rotation);
    public ActionContext AsActionContext();
    public Task SendComposerToRoomAsync(IComposer composer);
}
