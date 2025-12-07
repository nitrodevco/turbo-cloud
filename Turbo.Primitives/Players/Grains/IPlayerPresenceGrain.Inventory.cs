using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Inventory.Furniture;

namespace Turbo.Primitives.Players.Grains;

public partial interface IPlayerPresenceGrain
{
    public Task OpenFurnitureInventoryAsync(CancellationToken ct);
    public Task OnFurnitureAddedAsync(IFurnitureItem item, CancellationToken ct);
    public Task OnFurnitureRemovedAsync(int itemId, CancellationToken ct);
}
