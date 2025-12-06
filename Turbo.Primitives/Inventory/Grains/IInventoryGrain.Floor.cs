using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Inventory.Snapshots;

namespace Turbo.Primitives.Inventory.Grains;

public partial interface IInventoryGrain
{
    public Task SendFurniToPlayerAsync(CancellationToken ct);
    public Task<FurnitureFloorItemSnapshot?> GetFloorItemSnapshotAsync(
        int itemId,
        CancellationToken ct
    );
    public Task<ImmutableArray<FurnitureFloorItemSnapshot>> GetAllFloorItemSnapshotsAsync(
        CancellationToken ct
    );
}
