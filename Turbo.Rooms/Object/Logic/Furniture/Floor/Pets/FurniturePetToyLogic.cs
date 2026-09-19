using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Pets.Enums;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Pets;

/// <summary>A toy: the pet plays with it and learns a little.</summary>
[RoomObjectLogic("pet_toy")]
public class FurniturePetToyLogic(IStuffDataFactory stuffDataFactory, IRoomFloorItemContext ctx)
    : FurniturePetSupplyLogic(stuffDataFactory, ctx)
{
    public override PetSupplyType SupplyType => PetSupplyType.Toy;

    protected override Task ServeAsync(IRoomPet pet, CancellationToken ct)
    {
        var config = _roomGrain._petConfig;

        pet.AddStatus(AvatarStatusType.Play, string.Empty);
        pet.ActionExpiresAtMs = _roomGrain.NowMs() + config.ActionDurationMs;

        return _roomGrain.PetModule.AddExperienceAsync(pet, config.ToyExperience, ct);
    }
}
