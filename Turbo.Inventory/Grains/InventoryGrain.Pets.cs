using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Pets.Snapshots;
using Turbo.Primitives.Rooms;

namespace Turbo.Inventory.Grains;

internal sealed partial class InventoryGrain
{
    public Task<ImmutableArray<PetSnapshot>> GetAllPetSnapshotsAsync(CancellationToken ct) =>
        _petModule.GetAllAsync(ct);

    public Task<PetSnapshot?> GetPetSnapshotAsync(int petId, CancellationToken ct) =>
        _petModule.GetAsync(petId, ct);

    public Task<PetSnapshot?> TryCheckOutPetAsync(int petId, RoomId roomId, CancellationToken ct) =>
        _petModule.TryCheckOutAsync(petId, roomId, ct);

    public Task<bool> ReturnPetAsync(PetSnapshot snapshot, CancellationToken ct) =>
        _petModule.ReturnAsync(snapshot, ct);

    public Task<PetSnapshot?> CreatePetAsync(
        string name,
        int typeId,
        int paletteId,
        int breedId,
        string color,
        int rarityLevel,
        CancellationToken ct
    ) => _petModule.CreateAsync(name, typeId, paletteId, breedId, color, rarityLevel, ct);

    public Task<bool> DeletePetAsync(int petId, CancellationToken ct) =>
        _petModule.DeleteAsync(petId, ct);
}
