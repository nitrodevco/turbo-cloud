using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Pets;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Pets;

/// <summary>A saddle: used on the owner's horse it is strapped on and the item is consumed.</summary>
[RoomObjectLogic("pet_saddle")]
public class FurniturePetSaddleLogic(IStuffDataFactory stuffDataFactory, IRoomFloorItemContext ctx)
    : FurniturePetProductLogic(stuffDataFactory, ctx)
{
    protected override string? GetRefusal(IRoomPet pet) =>
        pet.TypeId != PetTypes.HORSE ? "not a horse"
        : pet.HasSaddle ? "already saddled"
        : null;

    protected override async Task<bool> ApplyAsync(IRoomPet pet, CancellationToken ct)
    {
        await _roomGrain.PetModule.SetSaddleAsync(pet, true, ct);

        return true;
    }
}
