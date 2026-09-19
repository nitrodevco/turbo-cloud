using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.Interactions;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Pets;

/// <summary>
/// A furni that is used up on a pet (saddle, dye, custom part, fertilizer, revival potion).
/// The owner uses it with one of their own pets in the room; when the product takes effect the
/// item is consumed. A product only says which pet it refuses and what it does to the rest.
/// </summary>
public abstract class FurniturePetProductLogic(
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureFloorLogic(stuffDataFactory, ctx)
{
    public override FurnitureUsageType GetUsagePolicy() => FurnitureUsageType.Nobody;

    public override Task OnUseAsync(ActionContext ctx, int param, CancellationToken ct) =>
        Task.CompletedTask;

    /// <summary>
    /// Reads what the product needs from its extra data before a pet is looked at. Returns the
    /// reason it cannot be used at all, or null.
    /// </summary>
    protected virtual string? Prepare() => null;

    /// <summary>Why this pet cannot take the product, or null when it can.</summary>
    protected abstract string? GetRefusal(IRoomPet pet);

    /// <summary>Applies the product. False leaves the item unused.</summary>
    protected abstract Task<bool> ApplyAsync(IRoomPet pet, CancellationToken ct);

    /// <summary>Logged when <see cref="ApplyAsync"/> declines.</summary>
    protected virtual string ApplyRefusal => "the product had no effect on the pet";

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

        if (Prepare() is { } unusable)
            return Reject(ctx, interaction, unusable);

        if (!_roomGrain.PetModule.TryGetPet(use.PetId, out var pet) || pet.OwnerId != ctx.PlayerId)
            return Reject(ctx, interaction, "no pet of the owner with that id");

        if (GetRefusal(pet) is { } refusal)
            return Reject(ctx, interaction, refusal);

        if (!await ApplyAsync(pet, ct))
            return Reject(ctx, interaction, ApplyRefusal);

        await _roomGrain.ActionModule.DeleteItemByIdAsync(ctx, _ctx.ObjectId, ct);

        return true;
    }
}
