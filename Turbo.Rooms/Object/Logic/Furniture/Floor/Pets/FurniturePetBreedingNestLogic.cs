using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Pets;

/// <summary>
/// A breeding nest. Two pets of the same type told to breed walk onto it; once both are here
/// the room asks their owners to confirm and name the offspring.
/// </summary>
[RoomObjectLogic("pet_breeding_nest")]
public class FurniturePetBreedingNestLogic(
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureFloorLogic(stuffDataFactory, ctx)
{
    public const int CAPACITY = 2;

    private readonly List<int> _occupantPetIds = [];

    public IReadOnlyList<int> OccupantPetIds => _occupantPetIds;

    public override FurnitureUsageType GetUsagePolicy() => FurnitureUsageType.Nobody;

    public override bool CanWalk() => true;

    /// <summary>Whether the nest has room for this pet, and any pet already in it is of the same type.</summary>
    public bool CanAccept(IRoomPet pet)
    {
        if (_occupantPetIds.Contains(pet.PetId))
            return true;

        if (_occupantPetIds.Count >= CAPACITY)
            return false;

        foreach (var occupantId in _occupantPetIds)
        {
            if (
                _roomGrain.PetModule.TryGetPet(occupantId, out var occupant)
                && occupant.TypeId != pet.TypeId
            )
                return false;
        }

        return true;
    }

    public bool AddOccupant(IRoomPet pet)
    {
        if (!CanAccept(pet))
            return false;

        if (!_occupantPetIds.Contains(pet.PetId))
            _occupantPetIds.Add(pet.PetId);

        return true;
    }

    public void RemoveOccupant(int petId) => _occupantPetIds.Remove(petId);

    public override async Task OnPickupAsync(ActionContext ctx, CancellationToken ct)
    {
        await _roomGrain.PetModule.OnNestRemovedAsync(_ctx.ObjectId, ct);

        _occupantPetIds.Clear();

        await base.OnPickupAsync(ctx, ct);
    }
}
