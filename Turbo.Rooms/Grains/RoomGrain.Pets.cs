using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
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
        RunPetActionAsync(ctx, petId, "place", () => PetModule.PlacePetAsync(ctx, petId, x, y, ct));

    public Task<bool> MovePetAsync(
        ActionContext ctx,
        int petId,
        int x,
        int y,
        Rotation rotation,
        CancellationToken ct
    ) =>
        RunPetActionAsync(
            ctx,
            petId,
            "move",
            () => PetModule.MovePetAsync(ctx, petId, x, y, rotation, ct)
        );

    public Task<bool> PickupPetAsync(ActionContext ctx, int petId, CancellationToken ct) =>
        RunPetActionAsync(ctx, petId, "pick up", () => PetModule.PickupPetAsync(ctx, petId, ct));

    public Task<bool> SelectPetAsync(ActionContext ctx, int petId, CancellationToken ct) =>
        RunPetActionAsync(ctx, petId, "select", () => PetModule.SelectPetAsync(ctx, petId, ct));

    public Task<bool> RequestPetInfoAsync(ActionContext ctx, int petId, CancellationToken ct) =>
        RunPetActionAsync(
            ctx,
            petId,
            "get info of",
            () => PetModule.RequestPetInfoAsync(ctx, petId, ct)
        );

    public Task<bool> RequestPetCommandsAsync(ActionContext ctx, int petId, CancellationToken ct) =>
        RunPetActionAsync(
            ctx,
            petId,
            "get commands of",
            () => PetModule.RequestPetCommandsAsync(ctx, petId, ct)
        );

    public Task<bool> RespectPetAsync(ActionContext ctx, int petId, CancellationToken ct) =>
        RunPetActionAsync(ctx, petId, "respect", () => PetModule.RespectPetAsync(ctx, petId, ct));

    public Task<bool> MountPetAsync(
        ActionContext ctx,
        int petId,
        bool mount,
        CancellationToken ct
    ) =>
        RunPetActionAsync(
            ctx,
            petId,
            mount ? "mount" : "dismount",
            () => PetModule.MountPetAsync(ctx, petId, mount, ct)
        );

    public Task<bool> RemovePetSaddleAsync(ActionContext ctx, int petId, CancellationToken ct) =>
        RunPetActionAsync(
            ctx,
            petId,
            "unsaddle",
            () => PetModule.RemovePetSaddleAsync(ctx, petId, ct)
        );

    public Task<bool> TogglePetRidingPermissionAsync(
        ActionContext ctx,
        int petId,
        CancellationToken ct
    ) =>
        RunPetActionAsync(
            ctx,
            petId,
            "toggle riding permission of",
            () => PetModule.TogglePetRidingPermissionAsync(ctx, petId, ct)
        );

    public Task<bool> TogglePetBreedingPermissionAsync(
        ActionContext ctx,
        int petId,
        CancellationToken ct
    ) =>
        RunPetActionAsync(
            ctx,
            petId,
            "toggle breeding permission of",
            () => PetModule.TogglePetBreedingPermissionAsync(ctx, petId, ct)
        );

    public Task<bool> GivePetSupplementAsync(
        ActionContext ctx,
        int petId,
        PetSupplementType supplement,
        CancellationToken ct
    ) =>
        RunPetActionAsync(
            ctx,
            petId,
            "supplement",
            () => PetModule.GivePetSupplementAsync(ctx, petId, supplement, ct)
        );

    public Task<bool> HarvestPetAsync(ActionContext ctx, int petId, CancellationToken ct) =>
        RunPetActionAsync(ctx, petId, "harvest", () => PetModule.HarvestPetAsync(ctx, petId, ct));

    public Task<bool> CompostPetAsync(ActionContext ctx, int petId, CancellationToken ct) =>
        RunPetActionAsync(ctx, petId, "compost", () => PetModule.CompostPetAsync(ctx, petId, ct));

    public Task<bool> PassHandItemToPetAsync(ActionContext ctx, int petId, CancellationToken ct) =>
        RunPetActionAsync(
            ctx,
            petId,
            "pass a hand item to",
            () => PetModule.PassHandItemToPetAsync(ctx, petId, ct)
        );

    public Task<bool> BreedPetsAsync(
        ActionContext ctx,
        PetBreedingAction action,
        int petId,
        int otherPetId,
        CancellationToken ct
    ) =>
        RunPetActionAsync(
            ctx,
            petId,
            "breed",
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
        RunPetActionAsync(
            ctx,
            petId,
            "confirm nest breeding of",
            () => PetModule.ConfirmNestBreedingAsync(ctx, nestId, name, petId, otherPetId, ct)
        );

    public Task<bool> CancelNestBreedingAsync(
        ActionContext ctx,
        int nestId,
        CancellationToken ct
    ) =>
        RunPetActionAsync(
            ctx,
            nestId,
            "cancel nest breeding at",
            () => PetModule.CancelNestBreedingAsync(ctx, nestId, ct)
        );

    /// <summary>Every pet action marks the player active and is logged with the same shape on failure.</summary>
    private async Task<bool> RunPetActionAsync(
        ActionContext ctx,
        int petId,
        string action,
        Func<Task<bool>> body
    )
    {
        try
        {
            AvatarModule.TouchAvatar(ctx.PlayerId, NowMs());

            return await body();
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Player {PlayerId} failed to {Action} pet {PetId} in room {RoomId}",
                ctx.PlayerId,
                action,
                petId,
                _state.RoomId
            );

            return false;
        }
    }
}
