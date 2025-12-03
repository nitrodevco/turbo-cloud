using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Inventory.Furniture;
using Turbo.Primitives.Inventory.Snapshots;

namespace Turbo.Inventory.Grains;

public sealed partial class InventoryGrain
{
    public Task EnsureFurniLoadedAsync(CancellationToken ct) =>
        _furniModule.EnsureFurniLoadedAsync(ct);

    public Task<bool> AddFloorItemAsync(IFurnitureFloorItem item, CancellationToken ct) =>
        _furniModule.AddFloorItemAsync(item, ct);

    public Task<ImmutableArray<FurnitureFloorItemSnapshot>> GetAllFloorItemSnapshotsAsync(
        CancellationToken ct
    ) => _furniModule.GetAllFloorItemSnapshotsAsync(ct);
}
