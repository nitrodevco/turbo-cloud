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
/// A body dye: used on the owner's pet it moves the figure to the palette of the same breed
/// that carries the dye's colour tag (<see cref="PetDyeData"/>), and the item is consumed. The dye
/// is the item's or definition's extra data section, else the definition's <c>customparams</c> as
/// Habbo's furni data gives every body dye (<c>horse_dye_08</c> "15 Gold", <see cref="PetProductParams"/>).
/// </summary>
[RoomObjectLogic("pet_dye")]
public class FurniturePetDyeLogic(IStuffDataFactory stuffDataFactory, IRoomFloorItemContext ctx)
    : FurniturePetProductLogic(stuffDataFactory, ctx)
{
    private PetDyeData? _dye;

    protected override string ApplyRefusal => "no palette of that breed carries the colour";

    protected override string? Prepare()
    {
        _dye = FurnitureExtraDataSections.Read<PetDyeData>(
            _ctx.RoomObject.ExtraData,
            _ctx.Definition.ExtraData,
            PetDyeData.SECTION,
            _roomGrain._logger
        );

        if (_dye is null && PetProductParams.ParseDye(_ctx.Definition.CustomParams) is { } dye)
            _dye = new PetDyeData { PetTypeIds = [dye.PetTypeId], ColorTag = dye.ColorTag };

        return _dye is null ? "the item carries no dye data" : null;
    }

    protected override string? GetRefusal(IRoomPet pet) =>
        _dye!.PetTypeIds.Length > 0 && !_dye.PetTypeIds.Contains(pet.TypeId)
            ? "the dye does not fit this pet type"
            : null;

    protected override Task<bool> ApplyAsync(IRoomPet pet, CancellationToken ct) =>
        PetModule.DyeAsync(pet, _dye!.ColorTag, ct);
}
