using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Pets;

/// <summary>Fertilizer: used on the owner's growing monsterplant it grows a level at once.</summary>
[RoomObjectLogic("pet_fertilizer")]
public class FurniturePetFertilizerLogic(
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurniturePetProductLogic(stuffDataFactory, ctx)
{
    protected override string? GetRefusal(IRoomPet pet) =>
        !pet.IsMonsterplant ? "not a monsterplant"
        : pet.CanRevive || pet.Level >= _roomGrain._petConfig.MonsterplantMaxLevel
            ? "the plant is dead or fully grown"
        : null;

    protected override async Task<bool> ApplyAsync(IRoomPet pet, CancellationToken ct)
    {
        await _roomGrain.PetModule.FertilizeAsync(pet, ct);

        return true;
    }
}
