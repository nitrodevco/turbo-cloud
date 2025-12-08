using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Inventory.Furniture;
using Turbo.Primitives.Inventory.Snapshots;

namespace Turbo.Players.Grains;

public sealed partial class PlayerPresenceGrain
{
    public Task OpenFurnitureInventoryAsync(CancellationToken ct) =>
        _inventoryModule.OpenFurnitureInventoryAsync(ct);

    public Task OnFurnitureAddedAsync(FurnitureItemSnapshot snapshot, CancellationToken ct) =>
        _inventoryModule.OnFurnitureAddedAsync(snapshot, ct);

    public Task OnFurnitureRemovedAsync(int itemId, CancellationToken ct) =>
        _inventoryModule.OnFurnitureRemovedAsync(itemId, ct);
}
