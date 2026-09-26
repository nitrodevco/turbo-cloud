using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Pets.Enums;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Rooms.Grains;

public sealed partial class RoomGrain
{
    public Task<bool> PlacePetAsync(
        ActionContext ctx,
        int petId,
        int x,
        int y,
        CancellationToken ct
    ) =>
        RunLoggedAsync(
            ctx,
            "place pet",
            petId,
            () => PetModule.PlacePetAsync(ctx, petId, x, y, ct)
        );

    public Task<bool> MovePetAsync(
        ActionContext ctx,
        int petId,
        int x,
        int y,
        Rotation rotation,
        CancellationToken ct
    ) =>
        RunLoggedAsync(
            ctx,
            "move pet",
            petId,
            () => PetModule.MovePetAsync(ctx, petId, x, y, rotation, ct)
        );

    public Task<bool> PickupPetAsync(ActionContext ctx, int petId, CancellationToken ct) =>
        RunLoggedAsync(ctx, "pick up pet", petId, () => PetModule.PickupPetAsync(ctx, petId, ct));

    public Task<bool> SelectPetAsync(ActionContext ctx, int petId, CancellationToken ct) =>
        RunLoggedAsync(ctx, "select pet", petId, () => PetModule.SelectPetAsync(ctx, petId, ct));

    public Task<bool> RequestPetInfoAsync(ActionContext ctx, int petId, CancellationToken ct) =>
        RunLoggedAsync(
            ctx,
            "get info of pet",
            petId,
            () => PetModule.RequestPetInfoAsync(ctx, petId, ct)
        );

    public Task<bool> RequestPetCommandsAsync(ActionContext ctx, int petId, CancellationToken ct) =>
        RunLoggedAsync(
            ctx,
            "get commands of pet",
            petId,
            () => PetModule.RequestPetCommandsAsync(ctx, petId, ct)
        );

    public Task<bool> RespectPetAsync(ActionContext ctx, int petId, CancellationToken ct) =>
        RunLoggedAsync(ctx, "respect pet", petId, () => PetModule.RespectPetAsync(ctx, petId, ct));

    public Task<bool> MountPetAsync(
        ActionContext ctx,
        int petId,
        bool mount,
        CancellationToken ct
    ) =>
        RunLoggedAsync(
            ctx,
            mount ? "mount pet" : "dismount pet",
            petId,
            () => PetModule.MountPetAsync(ctx, petId, mount, ct)
        );

    public Task<bool> RemovePetSaddleAsync(ActionContext ctx, int petId, CancellationToken ct) =>
        RunLoggedAsync(
            ctx,
            "unsaddle pet",
            petId,
            () => PetModule.RemovePetSaddleAsync(ctx, petId, ct)
        );

    public Task<bool> TogglePetRidingPermissionAsync(
        ActionContext ctx,
        int petId,
        CancellationToken ct
    ) =>
        RunLoggedAsync(
            ctx,
            "toggle riding permission of pet",
            petId,
            () => PetModule.TogglePetRidingPermissionAsync(ctx, petId, ct)
        );

    public Task<bool> TogglePetBreedingPermissionAsync(
        ActionContext ctx,
        int petId,
        CancellationToken ct
    ) =>
        RunLoggedAsync(
            ctx,
            "toggle breeding permission of pet",
            petId,
            () => PetModule.TogglePetBreedingPermissionAsync(ctx, petId, ct)
        );

    public Task<bool> GivePetSupplementAsync(
        ActionContext ctx,
        int petId,
        PetSupplementType supplement,
        CancellationToken ct
    ) =>
        RunLoggedAsync(
            ctx,
            "supplement pet",
            petId,
            () => PetModule.GivePetSupplementAsync(ctx, petId, supplement, ct)
        );

    public Task<bool> HarvestPetAsync(ActionContext ctx, int petId, CancellationToken ct) =>
        RunLoggedAsync(ctx, "harvest pet", petId, () => PetModule.HarvestPetAsync(ctx, petId, ct));

    public Task<bool> CompostPetAsync(ActionContext ctx, int petId, CancellationToken ct) =>
        RunLoggedAsync(ctx, "compost pet", petId, () => PetModule.CompostPetAsync(ctx, petId, ct));

    public Task<bool> PassHandItemToPetAsync(ActionContext ctx, int petId, CancellationToken ct) =>
        RunLoggedAsync(
            ctx,
            "pass a hand item to pet",
            petId,
            () => PetModule.PassHandItemToPetAsync(ctx, petId, ct)
        );

    public Task<bool> BreedPetsAsync(
        ActionContext ctx,
        PetBreedingAction action,
        int petId,
        int otherPetId,
        CancellationToken ct
    ) =>
        RunLoggedAsync(
            ctx,
            "breed pet",
            petId,
            () => PetModule.BreedPetsAsync(ctx, action, petId, otherPetId, ct)
        );

    public Task<bool> ConfirmNestBreedingAsync(
        ActionContext ctx,
        int nestId,
        string name,
        int petId,
        int otherPetId,
        CancellationToken ct
    ) =>
        RunLoggedAsync(
            ctx,
            "confirm nest breeding of pet",
            petId,
            () => PetModule.ConfirmNestBreedingAsync(ctx, nestId, name, petId, otherPetId, ct)
        );

    public Task<bool> CancelNestBreedingAsync(
        ActionContext ctx,
        int nestId,
        CancellationToken ct
    ) =>
        RunLoggedAsync(
            ctx,
            "cancel the breeding in nest",
            nestId,
            () => PetModule.CancelNestBreedingAsync(ctx, nestId, ct)
        );
}
