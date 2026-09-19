using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Pets;

/// <summary>A revival potion: used on the owner's dead monsterplant it is watered back to life.</summary>
[RoomObjectLogic("pet_revive")]
public class FurniturePetReviveLogic(IStuffDataFactory stuffDataFactory, IRoomFloorItemContext ctx)
    : FurniturePetProductLogic(stuffDataFactory, ctx)
{
    protected override string? GetRefusal(IRoomPet pet) =>
        !pet.IsMonsterplant ? "not a monsterplant"
        : !pet.CanRevive ? "the plant is not dead"
        : null;

    protected override async Task<bool> ApplyAsync(IRoomPet pet, CancellationToken ct)
    {
        await _roomGrain.PetModule.ReviveAsync(pet, ct);

        return true;
    }
}
