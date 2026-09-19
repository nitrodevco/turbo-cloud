using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Pets.Enums;
using Turbo.Primitives.Rooms.Enums;

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
}
