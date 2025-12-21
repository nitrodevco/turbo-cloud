using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Inventory.Snapshots;
using Turbo.Primitives.Messages.Outgoing.Inventory.Furni;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Players.Grains.Modules;

internal sealed class PlayerInventoryModule(PlayerPresenceGrain presenceGrain)
{
    private readonly PlayerPresenceGrain _presenceGrain = presenceGrain;

    private bool _isFurnitureInventoryPrimed = false;

    public Task OnSessionAttachedAsync(CancellationToken ct)
    {
        _isFurnitureInventoryPrimed = false;
        return Task.CompletedTask;
    }

    public async Task OnSessionDetachedAsync(CancellationToken ct)
    {
        _isFurnitureInventoryPrimed = false;

        await _presenceGrain.SendComposerAsync(new FurniListInvalidateEventMessageComposer());
    }

    public async Task OpenFurnitureInventoryAsync(CancellationToken ct)
    {
        var inventoryGrain = _presenceGrain._grainFactory.GetInventoryGrain(
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
                await _presenceGrain.SendComposerAsync(
                    new FurniListEventMessageComposer
                    {
                        TotalFragments = totalFragments,
                        CurrentFragment = currentFragment,
                        Items = [.. fragmentItems],
                    }
                );

                fragmentItems.Clear();
                count = 0;
                currentFragment++;
            }
        }

        if (count <= 0)
            return;

        await _presenceGrain.SendComposerAsync(
            new FurniListEventMessageComposer
            {
                TotalFragments = totalFragments,
                CurrentFragment = currentFragment,
                Items = [.. fragmentItems],
            }
        );

        _isFurnitureInventoryPrimed = true;
    }

    public async Task OnFurnitureAddedAsync(FurnitureItemSnapshot snapshot, CancellationToken ct)
    {
        if (!_isFurnitureInventoryPrimed)
            return;

        await _presenceGrain.SendComposerAsync(
            new FurniListAddOrUpdateEventMessageComposer { Item = snapshot }
        );
    }

    public async Task OnFurnitureRemovedAsync(RoomObjectId itemId, CancellationToken ct)
    {
        if (!_isFurnitureInventoryPrimed)
            return;

        await _presenceGrain.SendComposerAsync(
            new FurniListRemoveEventMessageComposer { ItemId = itemId }
        );
    }
}
