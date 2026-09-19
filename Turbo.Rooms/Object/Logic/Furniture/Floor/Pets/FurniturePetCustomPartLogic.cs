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
/// A hair style or dye for a pet: used on the owner's pet it replaces the custom part on its
/// layer (<see cref="PetCustomPartData"/>) and the item is consumed.
/// </summary>
[RoomObjectLogic("pet_custom_part")]
public class FurniturePetCustomPartLogic(
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureFloorLogic(stuffDataFactory, ctx)
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

        var part = FurnitureExtraDataSections.Read<PetCustomPartData>(
            _ctx.RoomObject.ExtraData,
            _ctx.Definition.ExtraData,
            PetCustomPartData.SECTION,
            _roomGrain._logger
        );

        if (part is null)
        {
            _roomGrain._logger.LogWarning(
                "Pet customization item {ItemId} (definition {DefinitionId}) carries no part data",
                _ctx.ObjectId,
                _ctx.Definition.Id
            );

            return Reject(ctx, interaction, "no part data");
        }

        if (
            !_roomGrain.PetModule.TryGetPet(use.PetId, out var pet)
            || pet.OwnerId != ctx.PlayerId
            || (part.PetTypeIds.Length > 0 && !part.PetTypeIds.Contains(pet.TypeId))
        )
            return Reject(ctx, interaction, "no pet of the owner this part fits");

        await _roomGrain.ActionModule.DeleteItemByIdAsync(ctx, _ctx.ObjectId, ct);
        await _roomGrain.PetModule.SetCustomPartAsync(
            pet,
            part.LayerId,
            part.PartId,
            part.PaletteId,
            ct
        );

        return true;
    }
}
