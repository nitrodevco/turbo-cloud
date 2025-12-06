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

internal sealed partial class InventoryFurniModule(
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

        await LoadFurnitureAsync(ct);

        _state.IsFurniReady = true;
    }

    public async Task<bool> AddFloorItemAsync(IFurnitureFloorItem item, CancellationToken ct)
    {
        if (!_state.FloorItemsById.TryAdd(item.ItemId, item))
            throw new TurboException(TurboErrorCodeEnum.FloorItemNotFound);

        void func(int itemId) { }

        item.SetAction(func);

        if (_state.IsFurniReady)
            await _inventoryGrain.SendComposerAsync(
                new FurniListAddOrUpdateEventMessageComposer { Item = item.GetSnapshot() },
                ct
            );

        return true;
    }

    public Task<FurnitureFloorItemSnapshot?> GetFloorItemSnapshotAsync(
        int itemId,
        CancellationToken ct
    ) =>
        Task.FromResult(
            _state.FloorItemsById.TryGetValue(itemId, out var item) ? item.GetSnapshot() : null
        );

    public Task<ImmutableArray<FurnitureFloorItemSnapshot>> GetAllFloorItemSnapshotsAsync(
        CancellationToken ct
    ) =>
        Task.FromResult(
            _state.FloorItemsById.Values.Select(x => x.GetSnapshot()).ToImmutableArray()
        );

    private async Task LoadFurnitureAsync(CancellationToken ct)
    {
        _state.FloorItemsById.Clear();

        var (floorItems, wallItems) = await _furnitureItemsLoader.LoadByPlayerIdAsync(
            _inventoryGrain.GetPrimaryKeyLong(),
            ct
        );

        foreach (var item in floorItems)
            await AddFloorItemAsync(item, ct);

        foreach (var item in wallItems)
        {
            //
        }
    }
}
