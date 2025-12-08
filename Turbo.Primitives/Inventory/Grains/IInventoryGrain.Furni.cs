using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Inventory.Furniture;
using Turbo.Primitives.Inventory.Snapshots;
using Turbo.Primitives.Rooms.Snapshots;

namespace Turbo.Primitives.Inventory.Grains;

public partial interface IInventoryGrain
{
    public Task<bool> AddFurnitureAsync(IFurnitureItem item, CancellationToken ct);
    public Task<bool> AddFurnitureFromRoomItemSnapshotAsync(
        RoomItemSnapshot snapshot,
        CancellationToken ct
    );
    public Task<bool> RemoveFurnitureAsync(int itemId, CancellationToken ct);
    public Task<FurnitureItemSnapshot?> GetItemSnapshotAsync(int itemId, CancellationToken ct);
    public Task<ImmutableArray<FurnitureItemSnapshot>> GetAllItemSnapshotsAsync(
        CancellationToken ct
    );
}
