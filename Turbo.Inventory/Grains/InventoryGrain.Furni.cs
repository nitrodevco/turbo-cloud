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

    public Task<bool> AddFloorItemAsync(IFurnitureFloorItem item, CancellationToken ct) =>
        _furniModule.AddFloorItemAsync(item, ct);

    public async Task SendFurniToPlayerAsync(CancellationToken ct)
    {
        await EnsureFurniLoadedAsync(ct);

        var floorItems = await GetAllFloorItemSnapshotsAsync(ct);

        var totalFragments = (int)
            Math.Max(
                1,
                Math.Ceiling((double)floorItems.Length / _inventoryConfig.FurniPerFragment)
            );
        var currentFragment = 0;
        var count = 0;
        List<FurnitureFloorItemSnapshot> fragmentItems = [];

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

    public Task<FurnitureFloorItemSnapshot?> GetFloorItemSnapshotAsync(
        int itemId,
        CancellationToken ct
    ) => _furniModule.GetFloorItemSnapshotAsync(itemId, ct);

    public Task<ImmutableArray<FurnitureFloorItemSnapshot>> GetAllFloorItemSnapshotsAsync(
        CancellationToken ct
    ) => _furniModule.GetAllFloorItemSnapshotsAsync(ct);
}
