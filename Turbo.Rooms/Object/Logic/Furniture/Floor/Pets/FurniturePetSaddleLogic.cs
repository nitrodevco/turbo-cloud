using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.Interactions;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Pets;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Pets;

/// <summary>A saddle: used on the owner's horse it is strapped on and the item is consumed.</summary>
[RoomObjectLogic("pet_saddle")]
public class FurniturePetSaddleLogic(IStuffDataFactory stuffDataFactory, IRoomFloorItemContext ctx)
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

        if (
            !_roomGrain.PetModule.TryGetPet(use.PetId, out var pet)
            || pet.OwnerId != ctx.PlayerId
            || pet.TypeId != PetTypes.HORSE
        )
            return Reject(ctx, interaction, "no horse of the owner with that id");

        if (pet.HasSaddle)
            return Reject(ctx, interaction, "already saddled");

        await _roomGrain.ActionModule.DeleteItemByIdAsync(ctx, _ctx.ObjectId, ct);
        await _roomGrain.PetModule.SetSaddleAsync(pet, true, ct);

        return true;
    }
}
