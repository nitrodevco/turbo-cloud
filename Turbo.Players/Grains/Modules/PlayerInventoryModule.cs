using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Inventory.Furniture;
using Turbo.Primitives.Inventory.Grains;
using Turbo.Primitives.Inventory.Snapshots;
using Turbo.Primitives.Messages.Outgoing.Inventory.Furni;

namespace Turbo.Players.Grains.Modules;

internal sealed class PlayerInventoryModule(
    PlayerPresenceGrain presenceGrain,
    IGrainFactory grainFactory
)
{
    private readonly PlayerPresenceGrain _presenceGrain = presenceGrain;
    private readonly IGrainFactory _grainFactory = grainFactory;

    private bool _isFurnitureInventoryPrimed = false;

    public void OnSessionAttached()
    {
        _isFurnitureInventoryPrimed = false;
    }

    public void OnSessionDetached()
    {
        _isFurnitureInventoryPrimed = false;
    }

    public async Task OpenFurnitureInventoryAsync(CancellationToken ct)
    {
        var inventoryGrain = _grainFactory.GetGrain<IInventoryGrain>(
            _presenceGrain.GetPrimaryKeyLong()
        );
        var items = await inventoryGrain.GetAllItemSnapshotsAsync(ct);
        var furniPerFragment = 100;

        var totalFragments = (int)
            Math.Max(1, Math.Ceiling((double)items.Length / furniPerFragment));
        var currentFragment = 0;
        var count = 0;
        List<FurnitureItemSnapshot> fragmentItems = [];

        foreach (var item in items)
        {
            fragmentItems.Add(item);

            count++;

            if (count == furniPerFragment)
            {
                await _presenceGrain
                    .SendComposerAsync(
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

        await _presenceGrain
            .SendComposerAsync(
                new FurniListEventMessageComposer
                {
                    TotalFragments = totalFragments,
                    CurrentFragment = currentFragment,
                    Items = [.. fragmentItems],
                },
                ct
            )
            .ConfigureAwait(false);

        _isFurnitureInventoryPrimed = true;
    }

    public async Task OnFurnitureAddedAsync(IFurnitureItem item, CancellationToken ct)
    {
        if (!_isFurnitureInventoryPrimed)
            return;

        await _presenceGrain.SendComposerAsync(
            new FurniListAddOrUpdateEventMessageComposer { Item = item.GetSnapshot() },
            ct
        );
    }

    public async Task OnFurnitureRemovedAsync(int itemId, CancellationToken ct)
    {
        if (!_isFurnitureInventoryPrimed)
            return;

        await _presenceGrain.SendComposerAsync(
            new FurniListRemoveEventMessageComposer { ItemId = -Math.Abs(itemId) },
            ct
        );
    }
}
