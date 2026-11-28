using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Messages.Outgoing.Inventory.Pets;
using Turbo.Primitives.Messages.Outgoing.Users;
using Turbo.Primitives.Movements;
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

    public Task<PetSnapshot?> TryCheckOutPetAsync(
        int petId,
        RoomId roomId,
        MovementCause cause,
        CancellationToken ct
    ) => PetModule.TryCheckOutAsync(petId, roomId, cause, ct);

    public Task<bool> ReturnPetAsync(
        PetSnapshot snapshot,
        MovementCause cause,
        CancellationToken ct
    ) => PetModule.ReturnAsync(snapshot, cause, ct);

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
    ) =>
        PetModule.CreateAsync(
            name,
            typeId,
            paletteId,
            breedId,
            color,
            rarityLevel,
            customParts,
            cause,
            ct
        );

    public Task<bool> DeletePetAsync(int petId, MovementCause cause, CancellationToken ct) =>
        PetModule.DeleteAsync(petId, cause, ct);

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

    public async Task SendPetInventoryWithLocationsAsync(CancellationToken ct)
    {
        var placed = await PetModule.GetPlacedAsync(ct);
        var rooms = await _navigatorService.GetRoomsByIdsAsync(
            [.. placed.Select(x => x.RoomId!.Value).Distinct()],
            ct
        );
        var pets = (await GetAllPetSnapshotsAsync(ct)).Concat(placed).OrderBy(x => x.Id).ToList();

        await Presence.SendComposerAsync(
            ComposerFragments.Build(
                pets,
                _inventoryConfig.PetInventoryFragmentSize,
                (total, current, fragment) =>
                    new PetInventoryEventMessageComposer
                    {
                        TotalFragments = total,
                        CurrentFragment = current,
                        Pets = fragment,
                        Rooms =
                        [
                            .. fragment
                                .Where(x => x.RoomId is not null)
                                .Select(x => x.RoomId!.Value)
                                .Distinct()
                                .Where(rooms.ContainsKey)
                                .Select(x => new PetRoomSnapshot
                                {
                                    RoomId = x,
                                    Name = rooms[x].Name,
                                    Description = rooms[x].Description,
                                }),
                        ],
                    }
            ),
            ct
        );
    }

    public Task SendPetRetirementPriceAsync(CancellationToken ct) =>
        _grainFactory.SendComposerToPlayerAsync(
            PlayerId,
            new PetRetirementPriceMessageComposer
            {
                Credits = _inventoryConfig.PetRetirementCreditsPrice,
            },
            ct
        );

    public Task<bool> RetirePetAsync(int petId, CancellationToken ct) =>
        PetModule.RetireAsync(petId, ct);

    public Task<bool> RenamePetAsync(int petId, string name, CancellationToken ct) =>
        PetModule.RenameAsync(petId, name, ct);
}
