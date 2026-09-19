using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Pets.Enums;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Pets;

/// <summary>A water bowl: each drink restores energy and empties the bowl a little.</summary>
[RoomObjectLogic("pet_drink")]
public class FurniturePetDrinkLogic(IStuffDataFactory stuffDataFactory, IRoomFloorItemContext ctx)
    : FurniturePetSupplyLogic(stuffDataFactory, ctx)
{
    public override PetSupplyType SupplyType => PetSupplyType.Drink;

    protected override bool IsConsumable => true;

    protected override Task ServeAsync(IRoomPet pet, CancellationToken ct)
    {
        var config = _roomGrain._petConfig;

        pet.SetEnergy(System.Math.Min(config.MaxEnergy, pet.Energy + config.DrinkEnergy));
        pet.AddStatus(AvatarStatusType.Eat, string.Empty);
        pet.ActionExpiresAtMs = _roomGrain.NowMs() + config.ActionDurationMs;
        pet.MarkDirty();

        return _roomGrain.PetModule.PersistAsync(pet, ct);
    }
}
