using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Pets.Snapshots;
using Turbo.Primitives.Rooms;

namespace Turbo.Primitives.Inventory.Grains;

public partial interface IInventoryGrain
{
    public Task<ImmutableArray<PetSnapshot>> GetAllPetSnapshotsAsync(CancellationToken ct);
    public Task<PetSnapshot?> GetPetSnapshotAsync(int petId, CancellationToken ct);

    /// <summary>
    /// Hands a pet over to a room: the row is marked as standing in it and the pet leaves the
    /// inventory list. Null when the pet is not in this inventory.
    /// </summary>
    public Task<PetSnapshot?> TryCheckOutPetAsync(int petId, RoomId roomId, CancellationToken ct);

    /// <summary>Takes a pet back from a room with the stats it accumulated there.</summary>
    public Task<bool> ReturnPetAsync(PetSnapshot snapshot, CancellationToken ct);

    /// <summary>
    /// Creates a new pet (purchase, hatched package, nest breeding) in this inventory. The
    /// palette is the colour variant drawn; the breed names it.
    /// </summary>
    public Task<PetSnapshot?> CreatePetAsync(
        string name,
        int typeId,
        int paletteId,
        int breedId,
        string color,
        int rarityLevel,
        CancellationToken ct
    );
    public Task<bool> DeletePetAsync(int petId, CancellationToken ct);
}
