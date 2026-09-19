using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Outgoing.Room.Pets;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Pets;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object.Avatars;

namespace Turbo.Rooms.Grains.Modules;

public sealed partial class RoomPetModule
{
    /// <summary>
    /// Mounting needs the rider next to the pet. From further away the rider walks over and the
    /// mount completes on arrival; a rider who stops elsewhere gives up the attempt.
    /// </summary>
    public async Task<bool> MountPetAsync(
        ActionContext ctx,
        int petId,
        bool mount,
        CancellationToken ct
    )
    {
        if (!TryGetPet(petId, out var pet) || !TryGetPlayer(ctx.PlayerId, out var rider))
            return false;

        if (!mount)
        {
            if (pet.RiderObjectId != rider.ObjectId)
                return false;

            await DismountAsync(pet, ct);

            return true;
        }

        if (!pet.HasSaddle || pet.IsRiding || pet.IsMonsterplant)
            return false;

        if (pet.OwnerId != ctx.PlayerId && !pet.AnyoneCanRide)
            return false;

        if (Pets.Any(x => x.RiderObjectId == rider.ObjectId))
            return false;

        if (IsAdjacent(pet, rider))
        {
            await CompleteMountAsync(pet, rider, ct);

            return true;
        }

        pet.PendingRiderObjectId = rider.ObjectId;

        if (await WalkNextToAsync(rider, pet, ct))
            return true;

        pet.PendingRiderObjectId = -1;

        return false;
    }

    internal async Task TryCompletePendingMountAsync(IRoomPet pet, CancellationToken ct)
    {
        if (pet.PendingRiderObjectId <= 0 || pet.IsRiding)
            return;

        if (
            !_roomGrain._state.AvatarsByObjectId.TryGetValue(
                pet.PendingRiderObjectId,
                out var rider
            ) || rider.IsWalking
        )
        {
            if (rider is null)
                pet.PendingRiderObjectId = -1;

            return;
        }

        pet.PendingRiderObjectId = -1;

        if (IsAdjacent(pet, rider) && pet.HasSaddle)
            await CompleteMountAsync(pet, rider, ct);
    }

    private async Task CompleteMountAsync(IRoomPet pet, IRoomAvatar rider, CancellationToken ct)
    {
        await _roomGrain.AvatarModule.StopWalkingAsync(pet, ct);
        await _roomGrain.AvatarModule.StopWalkingAsync(rider, ct);

        ClearActionStatuses(pet);
        await LeaveNestAsync(pet, ct);

        pet.Sit(false);
        pet.Lay(false);
        pet.IsFreeRoaming = false;
        pet.FollowObjectId = -1;
        pet.TargetItemId = -1;

        rider.Sit(false);
        rider.Lay(false);

        _roomGrain.MapModule.RemoveAvatar(rider, false);

        rider.SetPosition(pet.X, pet.Y);

        _roomGrain.MapModule.AddAvatar(rider, false);
        _roomGrain.MapModule.UpdateHeightForAvatar(rider);

        rider.SetRotation(pet.Rotation);
        rider.MarkDirty();

        pet.SetRider(rider.ObjectId);

        await _roomGrain.AvatarModule.SetAvatarEffectAsync(
            rider.ObjectId,
            PetRiding.RIDER_EFFECT_ID,
            ct
        );
        await BroadcastFigureAsync(pet, ct);
        await PersistAsync(pet, ct);
    }

    internal async Task DismountAsync(IRoomPet pet, CancellationToken ct)
    {
        var riderObjectId = pet.RiderObjectId;

        pet.SetRider(-1);
        pet.IsFreeRoaming = true;

        if (_roomGrain._state.AvatarsByObjectId.TryGetValue(riderObjectId, out var rider))
        {
            await _roomGrain.AvatarModule.SetAvatarEffectAsync(rider.ObjectId, 0, ct);

            // Step the rider off the pet so the two do not share a tile.
            foreach (var (x, y) in TilesAround(pet.X, pet.Y))
            {
                var idx = _roomGrain.MapModule.ToIdx(x, y);

                if (!IsTileFreeForNpc(idx))
                    continue;

                _roomGrain.MapModule.RemoveAvatar(rider, false);

                rider.SetPosition(x, y);

                _roomGrain.MapModule.AddAvatar(rider, false);
                _roomGrain.MapModule.UpdateHeightForAvatar(rider);

                rider.MarkDirty();

                break;
            }
        }

        await BroadcastFigureAsync(pet, ct);
        await PersistAsync(pet, ct);
    }

    /// <summary>A ridden pet shadows its rider: same tile, height, facing and movement.</summary>
    internal async Task SyncRidingPetsAsync(CancellationToken ct)
    {
        foreach (var pet in Pets.Where(x => x.IsRiding).ToList())
        {
            if (!_roomGrain._state.AvatarsByObjectId.TryGetValue(pet.RiderObjectId, out var rider))
            {
                await DismountAsync(pet, ct);

                continue;
            }

            if (pet.X != rider.X || pet.Y != rider.Y)
            {
                _roomGrain.MapModule.RemoveAvatar(pet, false);

                pet.SetPosition(rider.X, rider.Y);

                _roomGrain.MapModule.AddAvatar(pet, false);
            }

            pet.SetHeight(rider.Z);
            pet.SetRotation(rider.Rotation);

            if (rider.Statuses.TryGetValue(AvatarStatusType.Move, out var move))
                pet.AddStatus(AvatarStatusType.Move, move);
            else
                pet.RemoveStatus(AvatarStatusType.Move);
        }
    }

    public async Task<bool> RemovePetSaddleAsync(ActionContext ctx, int petId, CancellationToken ct)
    {
        if (!TryGetPet(petId, out var pet) || pet.OwnerId != ctx.PlayerId || !pet.HasSaddle)
            return false;

        var definition = _roomGrain._definitionProvider.TryGetDefinitionByName(
            Config.SaddleDefinitionName
        );

        if (definition is null)
        {
            _roomGrain._logger.LogError(
                "Saddle definition {Name} is missing; the saddle of pet {PetId} stays on",
                Config.SaddleDefinitionName,
                petId
            );

            return false;
        }

        if (pet.IsRiding)
            await DismountAsync(pet, ct);

        await SetSaddleAsync(pet, false, ct);

        await _roomGrain
            ._grainFactory.GetInventoryGrain(ctx.PlayerId)
            .GrantFurnitureAsync(definition.Id, null, ct);

        return true;
    }

    internal async Task SetSaddleAsync(IRoomPet pet, bool hasSaddle, CancellationToken ct)
    {
        pet.SetSaddle(hasSaddle);

        await BroadcastFigureAsync(pet, ct);
        await PersistAsync(pet, ct);
        await SendInfoToOwnerAsync(pet, ct);
    }

    public async Task<bool> TogglePetRidingPermissionAsync(
        ActionContext ctx,
        int petId,
        CancellationToken ct
    )
    {
        if (!TryGetPet(petId, out var pet) || pet.OwnerId != ctx.PlayerId)
            return false;

        pet.SetAnyoneCanRide(!pet.AnyoneCanRide);

        await PersistAsync(pet, ct);
        await SendInfoToOwnerAsync(pet, ct);

        return true;
    }

    public async Task<bool> TogglePetBreedingPermissionAsync(
        ActionContext ctx,
        int petId,
        CancellationToken ct
    )
    {
        if (!TryGetPet(petId, out var pet) || pet.OwnerId != ctx.PlayerId)
            return false;

        pet.SetBreedingPermission(!pet.HasBreedingPermission);

        await BroadcastStatusAsync(pet, ct);
        await PersistAsync(pet, ct);
        await SendInfoToOwnerAsync(pet, ct);

        return true;
    }

    internal Task BroadcastFigureAsync(IRoomPet pet, CancellationToken ct) =>
        _roomGrain.SendComposerToRoomAsync(
            new PetFigureUpdateMessageComposer
            {
                ObjectId = pet.ObjectId,
                PetId = pet.PetId,
                Figure = pet.PetFigure,
                HasSaddle = pet.HasSaddle,
                IsRiding = pet.IsRiding,
            },
            ct
        );

    internal static bool IsAdjacent(IRoomAvatar a, IRoomAvatar b) =>
        Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y)) <= 1;
}
