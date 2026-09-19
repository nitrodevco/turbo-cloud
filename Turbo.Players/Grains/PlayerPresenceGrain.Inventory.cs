using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Bots.Snapshots;
using Turbo.Primitives.Inventory.Snapshots;
using Turbo.Primitives.Messages.Outgoing.Inventory.Bots;
using Turbo.Primitives.Messages.Outgoing.Inventory.Furni;
using Turbo.Primitives.Messages.Outgoing.Inventory.Pets;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Pets.Snapshots;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Players.Grains;

/// <summary>
/// The three inventory sections as the client sees them. Furniture and pets are sent in
/// fragments; bots fit one packet. An empty section still answers, so the client stops waiting.
/// </summary>
internal sealed partial class PlayerPresenceGrain
{
    public async Task OpenFurnitureInventoryAsync(CancellationToken ct)
    {
        var items = await _grainFactory
            .GetInventoryGrain(_state.PlayerId)
            .GetAllItemSnapshotsAsync(ct);

        await SendFragmentsAsync(
            items,
            _playerConfig.FurnitureInventoryFragmentSize,
            (total, current, fragment) =>
                new FurniListEventMessageComposer
                {
                    TotalFragments = total,
                    CurrentFragment = current,
                    Items = fragment,
                },
            ct
        );
    }

    public Task OnFurnitureAddedAsync(
        ImmutableArray<FurnitureItemSnapshot> items,
        CancellationToken ct
    ) =>
        items.IsDefaultOrEmpty
            ? Task.CompletedTask
            : SendComposerAsync(new FurniListInvalidateEventMessageComposer(), ct);

    public Task OnFurnitureRemovedAsync(ImmutableArray<RoomObjectId> itemIds, CancellationToken ct)
    {
        if (itemIds.IsDefaultOrEmpty)
            return Task.CompletedTask;

        IReadOnlyList<IComposer> composers =
        [
            .. itemIds.Select(itemId => new FurniListRemoveEventMessageComposer
            {
                ItemId = itemId,
            }),
        ];

        return SendComposerAsync(composers, ct);
    }

    public async Task OpenPetInventoryAsync(CancellationToken ct)
    {
        var pets = await _grainFactory
            .GetInventoryGrain(_state.PlayerId)
            .GetAllPetSnapshotsAsync(ct);

        await SendFragmentsAsync(
            pets,
            _playerConfig.PetInventoryFragmentSize,
            (total, current, fragment) =>
                new PetInventoryEventMessageComposer
                {
                    TotalFragments = total,
                    CurrentFragment = current,
                    Pets = fragment,
                },
            ct
        );
    }

    public Task OnPetAddedAsync(PetSnapshot snapshot, bool openInventory, CancellationToken ct) =>
        SendComposerAsync(
            new PetAddedToInventoryEventMessageComposer
            {
                Pet = snapshot,
                OpenInventory = openInventory,
            },
            ct
        );

    public Task OnPetRemovedAsync(int petId, CancellationToken ct) =>
        SendComposerAsync(new PetRemovedFromInventoryEventMessageComposer { PetId = petId }, ct);

    public async Task OpenBotInventoryAsync(CancellationToken ct)
    {
        var bots = await _grainFactory
            .GetInventoryGrain(_state.PlayerId)
            .GetAllBotSnapshotsAsync(ct);

        await SendComposerAsync(new BotInventoryEventMessageComposer { Bots = bots }, ct);
    }

    public Task OnBotAddedAsync(BotSnapshot snapshot, bool openInventory, CancellationToken ct) =>
        SendComposerAsync(
            new BotAddedToInventoryEventMessageComposer
            {
                Bot = snapshot,
                OpenInventory = openInventory,
            },
            ct
        );

    public Task OnBotRemovedAsync(int botId, CancellationToken ct) =>
        SendComposerAsync(new BotRemovedFromInventoryEventMessageComposer { BotId = botId }, ct);

    /// <summary>
    /// Sends a list in fragments of at most <paramref name="perFragment"/> entries, in one
    /// batch. There is always at least one fragment, empty when the list is.
    /// </summary>
    private Task SendFragmentsAsync<T>(
        ImmutableArray<T> entries,
        int perFragment,
        Func<int, int, ImmutableArray<T>, IComposer> createFragment,
        CancellationToken ct
    )
    {
        perFragment = Math.Max(1, perFragment);

        var totalFragments = Math.Max(1, (entries.Length + perFragment - 1) / perFragment);
        var composers = new List<IComposer>(totalFragments);

        for (var fragment = 0; fragment < totalFragments; fragment++)
        {
            var start = fragment * perFragment;
            var length = Math.Min(perFragment, entries.Length - start);

            composers.Add(
                createFragment(
                    totalFragments,
                    fragment,
                    length > 0 ? entries.Slice(start, length) : []
                )
            );
        }

        return SendComposerAsync(composers, ct);
    }
}
