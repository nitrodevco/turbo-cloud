using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Inventory.Snapshots;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Furniture.Wall;

namespace Turbo.Primitives.Rooms.Object.Furniture;

public interface IRoomItemsLoader
{
    public Task<(
        IReadOnlyList<IRoomFloorItem>,
        IReadOnlyList<IRoomWallItem>,
        IReadOnlyDictionary<long, string>
    )> LoadByRoomIdAsync(long roomId, CancellationToken ct);
    public IRoomItem CreateFromFurnitureItemSnapshot(FurnitureItemSnapshot item);
}
