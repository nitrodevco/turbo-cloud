using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Bots.Snapshots;
using Turbo.Primitives.Inventory.Snapshots;
using Turbo.Primitives.Pets.Snapshots;
using Turbo.Primitives.Players.Snapshots;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.Players.Grains;

public partial interface IPlayerPresenceGrain
{
    public Task OpenFurnitureInventoryAsync(CancellationToken ct);

    /// <summary>Items joined the furniture inventory; one call per change, however many items.</summary>
    public Task OnFurnitureAddedAsync(
        ImmutableArray<FurnitureItemSnapshot> items,
        CancellationToken ct
    );

    /// <summary>Items left the furniture inventory; one call per change, however many items.</summary>
    public Task OnFurnitureRemovedAsync(ImmutableArray<RoomObjectId> itemIds, CancellationToken ct);
    public Task OpenPetInventoryAsync(CancellationToken ct);
    public Task OnPetAddedAsync(PetSnapshot snapshot, bool openInventory, CancellationToken ct);
    public Task OnPetRemovedAsync(int petId, CancellationToken ct);
    public Task OpenBotInventoryAsync(CancellationToken ct);
    public Task OnBotAddedAsync(BotSnapshot snapshot, bool openInventory, CancellationToken ct);
    public Task OnBotRemovedAsync(int botId, CancellationToken ct);

    // The badge calls only send what they are given. The inventory grain awaits them, so none of
    // them may call the inventory grain back.
    public Task SendBadgeInventoryAsync(
        ImmutableArray<PlayerBadgeSnapshot> badges,
        CancellationToken ct
    );
    public Task OnBadgeReceivedAsync(PlayerBadgeSnapshot badge, CancellationToken ct);

    /// <summary>What the player wears changed: the room they are in, or else they alone, is told.</summary>
    public Task OnSelectedBadgesChangedAsync(
        ImmutableArray<PlayerBadgeSnapshot> selectedBadges,
        CancellationToken ct
    );
}
