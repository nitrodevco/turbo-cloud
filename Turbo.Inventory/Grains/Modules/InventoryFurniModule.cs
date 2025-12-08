using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Logging;
using Turbo.Primitives;
using Turbo.Primitives.Inventory.Factories;
using Turbo.Primitives.Inventory.Furniture;
using Turbo.Primitives.Inventory.Snapshots;

namespace Turbo.Inventory.Grains.Modules;

internal sealed class InventoryFurniModule(
    InventoryGrain inventoryGrain,
    InventoryLiveState liveState,
    IInventoryFurnitureLoader furnitureItemsLoader
)
{
    private readonly InventoryGrain _inventoryGrain = inventoryGrain;
    private readonly InventoryLiveState _state = liveState;
    private readonly IInventoryFurnitureLoader _furnitureItemsLoader = furnitureItemsLoader;

    public async Task EnsureFurnitureReadyAsync(CancellationToken ct)
    {
        if (_state.IsFurnitureReady)
            return;

        await LoadFurnitureAsync(ct);

        _state.IsFurnitureReady = true;
    }

    public async Task<bool> AddFurnitureAsync(IFurnitureItem item, CancellationToken ct)
    {
        if (!_state.FurnitureById.TryAdd(item.ItemId, item))
            throw new TurboException(TurboErrorCodeEnum.FloorItemNotFound);

        item.SetAction(itemId => { });

        return true;
    }

    public async Task<bool> RemoveFurnitureAsync(int itemId, CancellationToken ct)
    {
        if (!_state.FurnitureById.Remove(itemId, out var item))
            return false;

        item.SetAction(null);

        return true;
    }

    public async Task<FurnitureItemSnapshot?> GetItemSnapshotAsync(int itemId, CancellationToken ct)
    {
        await EnsureFurnitureReadyAsync(ct);

        return _state.FurnitureById.TryGetValue(itemId, out var item) ? item.GetSnapshot() : null;
    }

    public async Task<ImmutableArray<FurnitureItemSnapshot>> GetAllItemSnapshotsAsync(
        CancellationToken ct
    )
    {
        await EnsureFurnitureReadyAsync(ct);

        return [.. _state.FurnitureById.Values.Select(x => x.GetSnapshot())];
    }

    private async Task LoadFurnitureAsync(CancellationToken ct)
    {
        _state.FurnitureById.Clear();

        var items = await _furnitureItemsLoader.LoadByPlayerIdAsync(
            _inventoryGrain.GetPrimaryKeyLong(),
            ct
        );

        foreach (var item in items)
            await AddFurnitureAsync(item, ct);
    }
}
