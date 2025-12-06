using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Inventory.Furniture;
using Turbo.Primitives.Inventory.Snapshots;
using Turbo.Primitives.Messages.Outgoing.Inventory.Furni;

namespace Turbo.Inventory.Grains;

public sealed partial class InventoryGrain
{
    public Task EnsureFurniLoadedAsync(CancellationToken ct) =>
        _furniModule.EnsureFurniLoadedAsync(ct);

    public Task<bool> AddItemAsync(IFurnitureItem item, CancellationToken ct) =>
        _furniModule.AddItemAsync(item, ct);

    public Task<bool> RemoveItemAsync(int itemId, CancellationToken ct) =>
        _furniModule.RemoveItemAsync(itemId, ct);

    public async Task SendItemsToPlayerAsync(CancellationToken ct)
    {
        await EnsureFurniLoadedAsync(ct);

        var floorItems = await GetAllItemSnapshotsAsync(ct);

        var totalFragments = (int)
            Math.Max(
                1,
                Math.Ceiling((double)floorItems.Length / _inventoryConfig.FurniPerFragment)
            );
        var currentFragment = 0;
        var count = 0;
        List<FurnitureItemSnapshot> fragmentItems = [];

        foreach (var item in floorItems)
        {
            fragmentItems.Add(item);

            count++;

            if (count == _inventoryConfig.FurniPerFragment)
            {
                await SendComposerAsync(
                        new FurniListEventMessageComposer
                        {
                            TotalFragments = totalFragments,
                            CurrentFragment = currentFragment,
                            Items = [.. fragmentItems],
                        },
                        ct
                    )
                    .ConfigureAwait(false);

                fragmentItems.Clear();
                count = 0;
                currentFragment++;
            }
        }

        if (count <= 0)
            return;

        await SendComposerAsync(
                new FurniListEventMessageComposer
                {
                    TotalFragments = totalFragments,
                    CurrentFragment = currentFragment,
                    Items = [.. fragmentItems],
                },
                ct
            )
            .ConfigureAwait(false);
    }

    public Task<FurnitureItemSnapshot?> GetItemSnapshotAsync(int itemId, CancellationToken ct) =>
        _furniModule.GetItemSnapshotAsync(itemId, ct);

    public Task<ImmutableArray<FurnitureItemSnapshot>> GetAllItemSnapshotsAsync(
        CancellationToken ct
    ) => _furniModule.GetAllItemSnapshotsAsync(ct);
}
