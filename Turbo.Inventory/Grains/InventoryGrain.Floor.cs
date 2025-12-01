using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Logging;
using Turbo.Primitives;
using Turbo.Primitives.Inventory.Furniture;
using Turbo.Primitives.Inventory.Snapshots;
using Turbo.Primitives.Messages.Outgoing.Inventory.Furni;

namespace Turbo.Inventory.Grains;

public sealed partial class InventoryGrain
{
    public async Task<bool> AddFloorItemAsync(IFurnitureFloorItem item, CancellationToken ct)
    {
        if (!_state.FloorItemsById.TryAdd(item.ItemId, item))
            throw new TurboException(TurboErrorCodeEnum.FloorItemNotFound);

        void func(int itemId) { }

        item.SetAction(func);

        _ = new FurniListAddOrUpdateEventMessageComposer { Item = item.GetSnapshot() };

        return true;
    }

    public Task<ImmutableArray<FurnitureFloorItemSnapshot>> GetAllFloorItemSnapshotsAsync(
        CancellationToken ct
    ) =>
        Task.FromResult(
            _state.FloorItemsById.Values.Select(x => x.GetSnapshot()).ToImmutableArray()
        );
}
