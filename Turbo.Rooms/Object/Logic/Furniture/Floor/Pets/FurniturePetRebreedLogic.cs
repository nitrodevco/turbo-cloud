using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Furniture.ExtraData;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Pets;

/// <summary>
/// A re-breeding potion: used on the owner's grown monsterplant that has bred, it may breed once
/// more. The client offers it only for a grown, living plant that cannot breed; the potion's
/// rarity band is <see cref="PetRebreedData"/>.
/// </summary>
[RoomObjectLogic("pet_rebreed")]
public class FurniturePetRebreedLogic(IStuffDataFactory stuffDataFactory, IRoomFloorItemContext ctx)
    : FurniturePetProductLogic(stuffDataFactory, ctx)
{
    private PetRebreedData _band = new();

    protected override string? Prepare()
    {
        _band =
            FurnitureExtraDataSections.Read<PetRebreedData>(
                _ctx.RoomObject.ExtraData,
                _ctx.Definition.ExtraData,
                PetRebreedData.SECTION,
                _roomGrain._logger
            ) ?? new PetRebreedData();

        return null;
    }

    protected override string? GetRefusal(IRoomPet pet)
    {
        if (!pet.IsMonsterplant)
            return "not a monsterplant";

        PetModule.RefreshFlags(pet);

        return pet.CanRevive || pet.Level < _roomGrain._petConfig.MonsterplantMaxLevel
                ? "the plant is dead or still growing"
            : !pet.HasBred ? "the plant can still breed"
            : pet.RarityLevel < _band.MinRarityLevel || pet.RarityLevel > _band.MaxRarityLevel
                ? "the potion does not fit the plant's rarity"
            : null;
    }

    protected override async Task<bool> ApplyAsync(IRoomPet pet, CancellationToken ct)
    {
        await PetModule.RebreedAsync(pet, ct);

        return true;
    }
}
