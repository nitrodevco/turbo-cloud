using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Inventory.Snapshots;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Object.Furniture;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Furniture.Wall;

namespace Turbo.Primitives.Rooms.Providers;

public interface IRoomItemsProvider
{
    public Task<(
        IReadOnlyList<IRoomFloorItem>,
        IReadOnlyList<IRoomWallItem>,
        IReadOnlyDictionary<PlayerId, string>
    )> LoadByRoomIdAsync(RoomId roomId, CancellationToken ct);
    public IRoomItem CreateFromFurnitureItemSnapshot(FurnitureItemSnapshot item);
}
