using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Inventory.Furniture;

namespace Turbo.Players.Grains;

public sealed partial class PlayerPresenceGrain
{
    public Task OpenFurnitureInventoryAsync(CancellationToken ct) =>
        _inventoryModule.OpenFurnitureInventoryAsync(ct);

    public Task OnFurnitureAddedAsync(IFurnitureItem item, CancellationToken ct) =>
        _inventoryModule.OnFurnitureAddedAsync(item, ct);

    public Task OnFurnitureRemovedAsync(int itemId, CancellationToken ct) =>
        _inventoryModule.OnFurnitureRemovedAsync(itemId, ct);
}
