using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Inventory.Snapshots;

namespace Turbo.Primitives.Players.Grains;

public partial interface IPlayerPresenceGrain
{
    public Task OpenFurnitureInventoryAsync(CancellationToken ct);
    public Task OnFurnitureAddedAsync(FurnitureItemSnapshot snapshot, CancellationToken ct);
    public Task OnFurnitureRemovedAsync(int itemId, CancellationToken ct);
}
