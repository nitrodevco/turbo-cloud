using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Catalog.Snapshots;
using Turbo.Primitives.Inventory.Furniture;
using Turbo.Primitives.Inventory.Snapshots;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Snapshots.Furniture;

namespace Turbo.Primitives.Inventory.Grains;

public partial interface IInventoryGrain
{
    public Task<bool> AddFurnitureAsync(IFurnitureItem item, CancellationToken ct);
    public Task<bool> AddFurnitureFromRoomItemSnapshotAsync(
        RoomItemSnapshot snapshot,
        CancellationToken ct
    );
    public Task<bool> RemoveFurnitureAsync(RoomObjectId itemId, CancellationToken ct);
    public Task GrantCatalogOfferAsync(
        CatalogOfferSnapshot offer,
        string extraParam,
        int quantity,
        CancellationToken ct
    );
    public Task<FurnitureItemSnapshot?> GetItemSnapshotAsync(
        RoomObjectId itemId,
        CancellationToken ct
    );
    public Task<ImmutableArray<FurnitureItemSnapshot>> GetAllItemSnapshotsAsync(
        CancellationToken ct
    );
}
