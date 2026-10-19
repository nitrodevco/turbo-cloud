using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.Interactions;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Movements;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Pets;

/// <summary>
/// A furni that is used up on a pet (saddle, dye, custom part, fertilizer, revival potion).
/// The owner uses it with one of their own pets in the room; when the product takes effect the
/// item is consumed. A product only says which pet it refuses and what it does to the rest.
/// <para>
/// It is used from two places, as Flash offers it: standing in the room (this item's
/// <see cref="OnInteractAsync"/>), or straight from the inventory's use button
/// (<c>RoomPetModule.UsePetProductFromInventoryAsync</c>), where the room gives the item this
/// logic without placing it. Both ask <see cref="TryGetPetToTake"/> and use
/// <see cref="TryApplyAsync"/>; each consumes the item where it is.
/// </para>
/// </summary>
public abstract class FurniturePetProductLogic(
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureFloorLogic(stuffDataFactory, ctx)
{
    public override FurnitureUsageType GetUsagePolicy() => FurnitureUsageType.Nobody;

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

        if (!IsOwnedOutrightBy(ctx))
            return Reject(ctx, interaction, "not the owner, or borrowed or temporary");

        if (!TryGetPetToTake(ctx, use, out var pet) || !await TryApplyAsync(ctx, use, pet, ct))
            return false;

        await ActionModule.DeleteItemByIdAsync(
            ctx,
            _ctx.ObjectId,
            MovementCause.New(MovementCauses.USE_PET_PRODUCT, ctx.PlayerId, _ctx.ObjectId.Value),
            ct
        );

        return true;
    }

    /// <summary>
    /// The acting player's pet in this room that the product may be used on, after the
    /// product read what it needs. False, with the refusal logged, when the product cannot be
    /// used at all, the pet is not theirs or not here, or the product does not fit it. Whether
    /// the item is the player's to use is the caller's to check first: it depends on where the
    /// item is.
    /// </summary>
    internal bool TryGetPetToTake(
        ActionContext ctx,
        UseWithPetInteraction use,
        [NotNullWhen(true)] out IRoomPet? pet
    )
    {
        pet = null;

        if (Prepare() is { } unusable)
            return Reject(ctx, use, unusable);

        if (!PetModule.TryGetPet(use.PetId, out var found) || found.OwnerId != ctx.PlayerId)
            return Reject(ctx, use, "no pet of the owner with that id");

        if (GetRefusal(found) is { } refusal)
            return Reject(ctx, use, refusal);

        pet = found;

        return true;
    }

    /// <summary>
    /// Applies the product to a pet <see cref="TryGetPetToTake"/> gave. False, logged, when it
    /// had no effect; the item is the caller's to consume only on true.
    /// </summary>
    internal async Task<bool> TryApplyAsync(
        ActionContext ctx,
        UseWithPetInteraction use,
        IRoomPet pet,
        CancellationToken ct
    ) => await ApplyAsync(pet, ct) || Reject(ctx, use, ApplyRefusal);
}
