using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Pets.Enums;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Pets;

/// <summary>A basket: the pet lies down on it and regains energy while it rests.</summary>
[RoomObjectLogic("pet_nest")]
public class FurniturePetNestLogic(IStuffDataFactory stuffDataFactory, IRoomFloorItemContext ctx)
    : FurniturePetSupplyLogic(stuffDataFactory, ctx)
{
    public override PetSupplyType SupplyType => PetSupplyType.Nest;

    public override bool CanWalk() => true;

    protected override Task ServeAsync(IRoomPet pet, CancellationToken ct)
    {
        pet.Lay(true, GetPostureOffset(), _ctx.RoomObject.Rotation);
        pet.IsFreeRoaming = false;

        return Task.CompletedTask;
    }
}
