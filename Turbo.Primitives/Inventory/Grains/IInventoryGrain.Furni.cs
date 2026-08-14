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

    /// <summary>
    /// Grant an LTD furniture item with serial number to the player's inventory.
    /// </summary>
    /// <param name="catalogProductId">The catalog product ID.</param>
    /// <param name="serialNumber">The unique serial number (e.g., 123).</param>
    /// <param name="seriesSize">The total series size (e.g., 500).</param>
    /// <param name="ct">Cancellation token.</param>
    public Task GrantLtdFurnitureAsync(
        int catalogProductId,
        int serialNumber,
        int seriesSize,
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
