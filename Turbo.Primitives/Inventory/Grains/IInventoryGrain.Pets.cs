using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Movements;
using Turbo.Primitives.Pets.Snapshots;
using Turbo.Primitives.Rooms;

namespace Turbo.Primitives.Inventory.Grains;

public partial interface IInventoryGrain
{
    /// <summary>Sends the player their pets tab, in fragments.</summary>
    public Task SendPetInventoryAsync(CancellationToken ct);

    /// <summary>
    /// The pets tab of a client naming
    /// <see cref="Networking.Features.ClientFeatures.PET_INVENTORY_LOCATIONS"/>: the pets in the
    /// inventory and, with the room each stands in, the player's pets placed in rooms.
    /// </summary>
    public Task SendPetInventoryWithLocationsAsync(CancellationToken ct);

    /// <summary>Tells the player what retiring a pet costs (<c>PetRetirementCreditsPrice</c>).</summary>
    public Task SendPetRetirementPriceAsync(CancellationToken ct);

    /// <summary>
    /// Retires a pet in this inventory: the price is charged, the pet deleted for good, and the
    /// player told the outcome. False, and nothing changed, when the pet is not in the inventory
    /// (placed in a room, or not the player's) or the player cannot pay.
    /// </summary>
    public Task<bool> RetirePetAsync(int petId, CancellationToken ct);

    /// <summary>
    /// Renames a pet in this inventory, under the rules a bought pet's name is held to. False
    /// when the name is refused or the pet is not in the inventory; a pet standing in a room is
    /// renamed by the room (<c>IRoomGrain.RenamePetAsync</c>).
    /// </summary>
    public Task<bool> RenamePetAsync(int petId, string name, CancellationToken ct);

    /// <summary>
    /// Tells the player whether a pet could be bought under this name: the same rules a pet
    /// purchase is held to, asked before the purchase is confirmed.
    /// </summary>
    public Task SendPetNameApprovalAsync(string name, CancellationToken ct);

    public Task<ImmutableArray<PetSnapshot>> GetAllPetSnapshotsAsync(CancellationToken ct);
    public Task<PetSnapshot?> GetPetSnapshotAsync(int petId, CancellationToken ct);

    /// <summary>
    /// Hands a pet over to a room: the row is marked as standing in it and the pet leaves the
    /// inventory list. Null when the pet is not in this inventory.
    /// </summary>
    public Task<PetSnapshot?> TryCheckOutPetAsync(
        int petId,
        RoomId roomId,
        MovementCause cause,
        CancellationToken ct
    );

    /// <summary>Takes a pet back from a room with the stats it accumulated there.</summary>
    public Task<bool> ReturnPetAsync(
        PetSnapshot snapshot,
        MovementCause cause,
        CancellationToken ct
    );

    /// <summary>
    /// Creates a new pet (purchase, hatched package, nest breeding) in this inventory. The
    /// palette is the colour variant drawn; the breed names it. The custom parts are the
    /// figure's layer, part and palette triples (a monsterplant's shape), empty for none.
    /// </summary>
    public Task<PetSnapshot?> CreatePetAsync(
        string name,
        int typeId,
        int paletteId,
        int breedId,
        string color,
        int rarityLevel,
        ImmutableArray<int> customParts,
        MovementCause cause,
        CancellationToken ct
    );
    public Task<bool> DeletePetAsync(int petId, MovementCause cause, CancellationToken ct);
}
