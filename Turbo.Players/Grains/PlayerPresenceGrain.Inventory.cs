using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Inventory.Snapshots;
using Turbo.Primitives.Messages.Outgoing.Inventory.Furni;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Players.Grains;

internal sealed partial class PlayerPresenceGrain
{
    public async Task OpenFurnitureInventoryAsync(CancellationToken ct)
    {
        var inventoryGrain = _grainFactory.GetInventoryGrain(_state.PlayerId);
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
                await SendComposerAsync(
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

        await SendComposerAsync(
            new FurniListEventMessageComposer
            {
                TotalFragments = totalFragments,
                CurrentFragment = currentFragment,
                Items = [.. fragmentItems],
            }
        );
    }

    public Task OnFurnitureAddedAsync(FurnitureItemSnapshot snapshot, CancellationToken ct) =>
        SendComposerAsync(new FurniListInvalidateEventMessageComposer());

    public Task OnFurnitureRemovedAsync(RoomObjectId itemId, CancellationToken ct) =>
        SendComposerAsync(new FurniListRemoveEventMessageComposer { ItemId = itemId });
}
