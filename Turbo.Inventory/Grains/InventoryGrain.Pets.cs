using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Messages.Outgoing.Inventory.Pets;
using Turbo.Primitives.Messages.Outgoing.Users;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Pets.Snapshots;
using Turbo.Primitives.Rooms;

namespace Turbo.Inventory.Grains;

internal sealed partial class InventoryGrain
{
    public Task<ImmutableArray<PetSnapshot>> GetAllPetSnapshotsAsync(CancellationToken ct) =>
        PetModule.GetAllAsync(ct);

    public Task<PetSnapshot?> GetPetSnapshotAsync(int petId, CancellationToken ct) =>
        PetModule.GetAsync(petId, ct);

    public Task<PetSnapshot?> TryCheckOutPetAsync(int petId, RoomId roomId, CancellationToken ct) =>
        PetModule.TryCheckOutAsync(petId, roomId, ct);

    public Task<bool> ReturnPetAsync(PetSnapshot snapshot, CancellationToken ct) =>
        PetModule.ReturnAsync(snapshot, ct);

    public Task<PetSnapshot?> CreatePetAsync(
        string name,
        int typeId,
        int paletteId,
        int breedId,
        string color,
        int rarityLevel,
        CancellationToken ct
    ) => PetModule.CreateAsync(name, typeId, paletteId, breedId, color, rarityLevel, ct);

    public Task<bool> DeletePetAsync(int petId, CancellationToken ct) =>
        PetModule.DeleteAsync(petId, ct);

    public Task SendPetNameApprovalAsync(string name, CancellationToken ct)
    {
        var (result, info) = PetModule.ApproveName(name);

        return _grainFactory.SendComposerToPlayerAsync(
            PlayerId,
            new ApproveNameMessageComposer { Result = result, NameValidationInfo = info },
            ct
        );
    }

    public async Task SendPetInventoryAsync(CancellationToken ct) =>
        await Presence.SendComposerAsync(
            ComposerFragments.Build(
                await GetAllPetSnapshotsAsync(ct),
                _inventoryConfig.PetInventoryFragmentSize,
                (total, current, fragment) =>
                    new PetInventoryEventMessageComposer
                    {
                        TotalFragments = total,
                        CurrentFragment = current,
                        Pets = fragment,
                    }
            ),
            ct
        );
}
