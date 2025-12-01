using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Inventory.Snapshots;

namespace Turbo.Primitives.Inventory.Grains;

public partial interface IInventoryGrain
{
    public Task<ImmutableArray<FurnitureFloorItemSnapshot>> GetAllFloorItemSnapshotsAsync(
        CancellationToken ct
    );
}
