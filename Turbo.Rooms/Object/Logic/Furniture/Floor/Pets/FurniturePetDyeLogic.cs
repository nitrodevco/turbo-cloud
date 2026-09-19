using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Furniture.ExtraData;
using Turbo.Primitives.Furniture.Interactions;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Pets;

/// <summary>
/// A body dye: used on the owner's pet it moves the figure to the palette of the same breed
/// that carries the dye's colour tag (<see cref="PetDyeData"/>), and the item is consumed.
/// </summary>
[RoomObjectLogic("pet_dye")]
public class FurniturePetDyeLogic(IStuffDataFactory stuffDataFactory, IRoomFloorItemContext ctx)
    : FurnitureFloorLogic(stuffDataFactory, ctx)
{
    public override FurnitureUsageType GetUsagePolicy() => FurnitureUsageType.Nobody;

    public override Task OnUseAsync(ActionContext ctx, int param, CancellationToken ct) =>
        Task.CompletedTask;

    public override async Task<bool> OnInteractAsync(
        ActionContext ctx,
        FurnitureInteraction interaction,
        CancellationToken ct
    )
    {
        if (interaction is not UseWithPetInteraction use)
            return false;

        if (_ctx.RoomObject.OwnerId != ctx.PlayerId)
            return Reject(ctx, interaction, "not the owner");

        var dye = FurnitureExtraDataSections.Read<PetDyeData>(
            _ctx.RoomObject.ExtraData,
            _ctx.Definition.ExtraData,
            PetDyeData.SECTION,
            _roomGrain._logger
        );

        if (dye is null)
        {
            _roomGrain._logger.LogWarning(
                "Pet dye {ItemId} (definition {DefinitionId}) carries no colour tag",
                _ctx.ObjectId,
                _ctx.Definition.Id
            );

            return Reject(ctx, interaction, "no dye data");
        }

        if (
            !_roomGrain.PetModule.TryGetPet(use.PetId, out var pet)
            || pet.OwnerId != ctx.PlayerId
            || (dye.PetTypeIds.Length > 0 && !dye.PetTypeIds.Contains(pet.TypeId))
        )
            return Reject(ctx, interaction, "no pet of the owner this dye fits");

        if (!await _roomGrain.PetModule.DyeAsync(pet, dye.ColorTag, ct))
            return Reject(ctx, interaction, "no palette of that breed carries the colour");

        await _roomGrain.ActionModule.DeleteItemByIdAsync(ctx, _ctx.ObjectId, ct);

        return true;
    }
}
