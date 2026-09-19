using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.Interactions;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Pets;

/// <summary>Fertilizer: used on the owner's growing monsterplant it grows a level at once.</summary>
[RoomObjectLogic("pet_fertilizer")]
public class FurniturePetFertilizerLogic(
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

        if (
            !_roomGrain.PetModule.TryGetPet(use.PetId, out var pet)
            || pet.OwnerId != ctx.PlayerId
            || !pet.IsMonsterplant
        )
            return Reject(ctx, interaction, "no monsterplant of the owner with that id");

        if (pet.CanRevive || pet.Level >= _roomGrain._petConfig.MonsterplantMaxLevel)
            return Reject(ctx, interaction, "the plant is dead or fully grown");

        await _roomGrain.ActionModule.DeleteItemByIdAsync(ctx, _ctx.ObjectId, ct);
        await _roomGrain.PetModule.FertilizeAsync(pet, ct);

        return true;
    }
}
