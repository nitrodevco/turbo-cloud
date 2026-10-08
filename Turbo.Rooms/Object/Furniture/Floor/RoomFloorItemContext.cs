using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic.Furniture;
using Turbo.Primitives.Rooms.Snapshots.Mapping;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Object.Furniture.Floor;

public sealed class RoomFloorItemContext(RoomGrain roomGrain, IRoomFloorItem roomObject)
    : RoomItemContext<IRoomFloorItem, IFurnitureFloorLogic, IRoomFloorItemContext>(
        roomGrain,
        roomObject
    ),
        IRoomFloorItemContext
{
    public int GetTileIdx() => _roomGrain.ToIdx(RoomObject.X, RoomObject.Y);

    public int GetTileIdx(int x, int y) => _roomGrain.ToIdx(x, y);

    // Every tile it covers: what a state change alters (a gate opening, a 2x2 hole closing)
    // holds on all of them, not only where the item stands.
    public void RefreshTile()
    {
        foreach (var (x, y) in FloorFootprint.Of(RoomObject).Tiles())
            _roomGrain.ComputeTile(x, y);
    }

    public Task<RoomTileSnapshot> GetTileSnapshotAsync(CancellationToken ct) =>
        _roomGrain.GetTileSnapshotAsync(RoomObject.X, RoomObject.Y, ct);
}
