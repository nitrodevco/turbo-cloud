using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Logging;
using Turbo.Primitives;
using Turbo.Primitives.Inventory.Furniture;
using Turbo.Primitives.Inventory.Snapshots;
using Turbo.Primitives.Messages.Outgoing.Inventory.Furni;

namespace Turbo.Inventory.Grains.Modules;

internal sealed class InventoryFurniModule(
    InventoryGrain inventoryGrain,
    InventoryLiveState liveState,
    IFurnitureItemsLoader furnitureItemsLoader
)
{
    private readonly InventoryGrain _inventoryGrain = inventoryGrain;
    private readonly InventoryLiveState _state = liveState;
    private readonly IFurnitureItemsLoader _furnitureItemsLoader = furnitureItemsLoader;

    public async Task EnsureFurniLoadedAsync(CancellationToken ct)
    {
        if (_state.IsFurniReady)
            return;

        await LoadItemsAsync(ct);

        _state.IsFurniReady = true;
    }

    public async Task<bool> AddItemAsync(IFurnitureItem item, CancellationToken ct)
    {
        if (!_state.FurnitureById.TryAdd(item.ItemId, item))
            throw new TurboException(TurboErrorCodeEnum.FloorItemNotFound);

        item.SetAction(itemId => { });

        if (_state.IsFurniReady)
            await _inventoryGrain.SendComposerAsync(
                new FurniListAddOrUpdateEventMessageComposer { Item = item.GetSnapshot() },
                ct
            );

        return true;
    }

    public async Task<bool> RemoveItemAsync(int itemId, CancellationToken ct)
    {
        if (!_state.FurnitureById.Remove(itemId, out var item))
            return false;

        if (_state.IsFurniReady)
            await _inventoryGrain.SendComposerAsync(
                new FurniListRemoveEventMessageComposer { ItemId = -Math.Abs(itemId) },
                ct
            );

        return true;
    }

    public Task<FurnitureItemSnapshot?> GetItemSnapshotAsync(int itemId, CancellationToken ct) =>
        Task.FromResult(
            _state.FurnitureById.TryGetValue(itemId, out var item) ? item.GetSnapshot() : null
        );

    public Task<ImmutableArray<FurnitureItemSnapshot>> GetAllItemSnapshotsAsync(
        CancellationToken ct
    ) =>
        Task.FromResult(
            _state.FurnitureById.Values.Select(x => x.GetSnapshot()).ToImmutableArray()
        );

    private async Task LoadItemsAsync(CancellationToken ct)
    {
        _state.FurnitureById.Clear();

        var items = await _furnitureItemsLoader.LoadByPlayerIdAsync(
            _inventoryGrain.GetPrimaryKeyLong(),
            ct
        );

        foreach (var item in items)
            await AddItemAsync(item, ct);
    }
}
