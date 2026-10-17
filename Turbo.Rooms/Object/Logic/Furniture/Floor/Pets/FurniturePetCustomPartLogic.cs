using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Furniture.ExtraData;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Pets;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Pets;

/// <summary>
/// A hair style or dye for a pet: used on the owner's pet it replaces the custom part on its
/// layer (<see cref="PetCustomPartData"/>) and the item is consumed. Without that extra data section
/// the definition's <c>customparams</c> say it, as Habbo's furni data gives every hair dye and hair
/// style (<c>horse_hairdye_06</c> "15 2,3 54,54", <c>horse_hairstyle_3</c> "15 2,3 103,103 1,1"),
/// changing every layer they list (<see cref="PetProductParams.ApplyCustomParts"/>).
/// </summary>
[RoomObjectLogic("pet_custom_part")]
public class FurniturePetCustomPartLogic(
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurniturePetProductLogic(stuffDataFactory, ctx)
{
    private PetCustomPartData? _part;
    private int? _paramsPetType;

    protected override string? Prepare()
    {
        _part = FurnitureExtraDataSections.Read<PetCustomPartData>(
            _ctx.RoomObject.ExtraData,
            _ctx.Definition.ExtraData,
            PetCustomPartData.SECTION,
            _roomGrain._logger
        );

        _paramsPetType = _part is null
            ? PetProductParams.PetTypeOfCustomParts(_ctx.Definition.CustomParams)
            : null;

        return _part is null && _paramsPetType is null ? "the item carries no part data" : null;
    }

    protected override string? GetRefusal(IRoomPet pet) =>
        _part is not null
            ? _part.PetTypeIds.Length > 0 && !_part.PetTypeIds.Contains(pet.TypeId)
                ? "the part does not fit this pet type"
                : null
            : _paramsPetType != pet.TypeId
                ? "the part does not fit this pet type"
                : null;

    protected override async Task<bool> ApplyAsync(IRoomPet pet, CancellationToken ct)
    {
        if (_part is not null)
        {
            await PetModule.SetCustomPartAsync(
                pet,
                _part.LayerId,
                _part.PartId,
                _part.PaletteId,
                ct
            );

            return true;
        }

        if (
            PetProductParams.ApplyCustomParts(
                pet.PetFigure.CustomParts,
                _ctx.Definition.CustomParams
            )
            is not { } parts
        )
            return false;

        await PetModule.SetCustomPartsAsync(pet, parts, ct);

        return true;
    }
}
