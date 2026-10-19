using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Pets.Enums;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.Rooms.Grains;

public partial interface IRoomGrain
{
    /// <summary>
    /// Takes a pet out of the acting player's inventory and stands it on a tile. (0, 0) means
    /// any free tile, as the inventory's place button sends. Failures are reported to the
    /// player as a <c>PetPlacingError</c>.
    /// </summary>
    public Task<bool> PlacePetAsync(
        ActionContext ctx,
        int petId,
        int x,
        int y,
        CancellationToken ct
    );

    /// <summary>Moves or turns a placed monsterplant; other pets walk on their own.</summary>
    public Task<bool> MovePetAsync(
        ActionContext ctx,
        int petId,
        int x,
        int y,
        Rotation rotation,
        CancellationToken ct
    );
    public Task<bool> PickupPetAsync(ActionContext ctx, int petId, CancellationToken ct);

    /// <summary>The client selected the pet; keeps the player active and reports whether it exists.</summary>
    public Task<bool> SelectPetAsync(ActionContext ctx, int petId, CancellationToken ct);
    public Task<bool> RequestPetInfoAsync(ActionContext ctx, int petId, CancellationToken ct);
    public Task<bool> RequestPetCommandsAsync(ActionContext ctx, int petId, CancellationToken ct);
    public Task<bool> RespectPetAsync(ActionContext ctx, int petId, CancellationToken ct);
    public Task<bool> MountPetAsync(ActionContext ctx, int petId, bool mount, CancellationToken ct);
    public Task<bool> RemovePetSaddleAsync(ActionContext ctx, int petId, CancellationToken ct);
    public Task<bool> TogglePetRidingPermissionAsync(
        ActionContext ctx,
        int petId,
        CancellationToken ct
    );
    public Task<bool> TogglePetBreedingPermissionAsync(
        ActionContext ctx,
        int petId,
        CancellationToken ct
    );
    public Task<bool> GivePetSupplementAsync(
        ActionContext ctx,
        int petId,
        PetSupplementType supplement,
        CancellationToken ct
    );
    public Task<bool> HarvestPetAsync(ActionContext ctx, int petId, CancellationToken ct);
    public Task<bool> CompostPetAsync(ActionContext ctx, int petId, CancellationToken ct);

    /// <summary>Hands the player's carried item to an adjacent pet, which eats or drinks it.</summary>
    public Task<bool> PassHandItemToPetAsync(ActionContext ctx, int petId, CancellationToken ct);

    /// <summary>Monsterplant breeding between two plants in the room.</summary>
    public Task<bool> BreedPetsAsync(
        ActionContext ctx,
        PetBreedingAction action,
        int petId,
        int otherPetId,
        CancellationToken ct
    );

    /// <summary>One owner's answer to the nest breeding confirmation.</summary>
    public Task<bool> ConfirmNestBreedingAsync(
        ActionContext ctx,
        int nestId,
        string name,
        int petId,
        int otherPetId,
        CancellationToken ct
    );
    public Task<bool> CancelNestBreedingAsync(ActionContext ctx, int nestId, CancellationToken ct);

    /// <summary>
    /// Uses a pet product (a saddle, a dye, a revival potion) from the acting player's inventory
    /// on one of their pets in this room, as the inventory's use button does. The same rules
    /// hold as for one standing in the room; a rented item is refused. The item is used up when
    /// the product takes effect, and given back when it then has none. False when it was refused.
    /// </summary>
    public Task<bool> UsePetProductFromInventoryAsync(
        ActionContext ctx,
        RoomObjectId itemId,
        int petId,
        CancellationToken ct
    );
}
