using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Furniture.ExtraData;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Pets;

/// <summary>
/// A hair style or dye for a pet: used on the owner's pet it replaces the custom part on its
/// layer (<see cref="PetCustomPartData"/>) and the item is consumed.
/// </summary>
[RoomObjectLogic("pet_custom_part")]
public class FurniturePetCustomPartLogic(
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurniturePetProductLogic(stuffDataFactory, ctx)
{
    private PetCustomPartData? _part;

    protected override string? Prepare()
    {
        _part = FurnitureExtraDataSections.Read<PetCustomPartData>(
            _ctx.RoomObject.ExtraData,
            _ctx.Definition.ExtraData,
            PetCustomPartData.SECTION,
            _roomGrain._logger
        );

        return _part is null ? "the item carries no part data" : null;
    }

    protected override string? GetRefusal(IRoomPet pet) =>
        _part!.PetTypeIds.Length > 0 && !_part.PetTypeIds.Contains(pet.TypeId)
            ? "the part does not fit this pet type"
            : null;

    protected override async Task<bool> ApplyAsync(IRoomPet pet, CancellationToken ct)
    {
        await _roomGrain.PetModule.SetCustomPartAsync(
            pet,
            _part!.LayerId,
            _part.PartId,
            _part.PaletteId,
            ct
        );

        return true;
    }
}
