using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Inventory.Snapshots;
using Turbo.Primitives.Players.Grains;

namespace Turbo.Inventory.Grains;

public sealed partial class InventoryGrain
{
    public Task EnsureFurnitureReadyAsync(CancellationToken ct) =>
        _furniModule.EnsureFurnitureReadyAsync(ct);

    public async Task<bool> RemoveFurnitureAsync(int itemId, CancellationToken ct)
    {
        if (!await _furniModule.RemoveFurnitureAsync(itemId, ct))
            return false;

        var presence = _grainFactory.GetGrain<IPlayerPresenceGrain>(this.GetPrimaryKeyLong());

        await presence.OnFurnitureRemovedAsync(itemId, ct);

        return true;
    }

    public Task<FurnitureItemSnapshot?> GetItemSnapshotAsync(int itemId, CancellationToken ct) =>
        _furniModule.GetItemSnapshotAsync(itemId, ct);

    public Task<ImmutableArray<FurnitureItemSnapshot>> GetAllItemSnapshotsAsync(
        CancellationToken ct
    ) => _furniModule.GetAllItemSnapshotsAsync(ct);
}
