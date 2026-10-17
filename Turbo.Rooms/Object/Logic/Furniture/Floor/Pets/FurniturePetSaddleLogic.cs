using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Pets;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Pets;

/// <summary>
/// A saddle: used on the owner's horse it is strapped on and the item is consumed. The
/// definition's <c>customparams</c> say which part it draws on the pet and which effect its rider
/// wears (<see cref="PetProductParams.ParseSaddle"/>); the pet keeps which saddle it wears, to
/// give that one back when it is taken off.
/// </summary>
[RoomObjectLogic("pet_saddle")]
public class FurniturePetSaddleLogic(IStuffDataFactory stuffDataFactory, IRoomFloorItemContext ctx)
    : FurniturePetProductLogic(stuffDataFactory, ctx)
{
    private PetSaddleParams? _saddle;

    protected override string? Prepare()
    {
        _saddle = PetProductParams.ParseSaddle(_ctx.Definition.CustomParams);

        return null;
    }

    protected override string? GetRefusal(IRoomPet pet) =>
        pet.TypeId != (_saddle?.PetTypeId ?? PetTypes.HORSE)
            ? "the saddle does not fit this pet type"
        : pet.HasSaddle ? "already saddled"
        : null;

    protected override async Task<bool> ApplyAsync(IRoomPet pet, CancellationToken ct)
    {
        await PetModule.PutSaddleOnAsync(pet, _ctx.Definition.Id, _saddle, ct);

        return true;
    }
}
