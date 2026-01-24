using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Rooms.Object.Logic.Furniture;
using Turbo.Primitives.Rooms.Snapshots.Mapping;

namespace Turbo.Primitives.Rooms.Object.Furniture.Floor;

public interface IRoomFloorItemContext<out TObject, out TLogic, out TSelf>
    : IRoomItemContext<TObject, TLogic, TSelf>
    where TObject : IRoomFloorItem<TObject, TLogic, TSelf>
    where TSelf : IRoomFloorItemContext<TObject, TLogic, TSelf>
    where TLogic : IFurnitureFloorLogic<TObject, TLogic, TSelf>
{
    new TObject RoomObject { get; }
}

public interface IRoomFloorItemContext
    : IRoomItemContext<IRoomFloorItem, IFurnitureFloorLogic, IRoomFloorItemContext>
{
    new IRoomFloorItem RoomObject { get; }
    public int GetTileIdx();
    public int GetTileIdx(int x, int y);
    public void RefreshTile();
    public Task<RoomTileSnapshot> GetTileSnapshotAsync(CancellationToken ct);
}
