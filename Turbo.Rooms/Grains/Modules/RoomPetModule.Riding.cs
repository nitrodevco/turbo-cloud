using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.Snapshots;
using Turbo.Primitives.Messages.Outgoing.Room.Pets;
using Turbo.Primitives.Movements;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Pets;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
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
        if (
            !TryGetPet(petId, out var pet)
            || !AvatarModule.TryGetPlayer(ctx.PlayerId, out var rider)
        )
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

        if (RoomAvatarModule.AreAdjacent(pet, rider))
        {
            await CompleteMountAsync(pet, rider, ct);

            return true;
        }

        // The pet stops for its rider, else it roams off the tile the rider walks to.
        await AvatarModule.StopWalkingAsync(pet, ct);
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

        if (!AvatarModule.TryGetAvatar(pet.PendingRiderObjectId, out var rider))
        {
            pet.PendingRiderObjectId = -1;

            return;
        }

        if (rider.IsWalking)
            return;

        pet.PendingRiderObjectId = -1;

        if (RoomAvatarModule.AreAdjacent(pet, rider) && pet.HasSaddle)
            await CompleteMountAsync(pet, rider, ct);
    }

    private async Task CompleteMountAsync(IRoomPet pet, IRoomAvatar rider, CancellationToken ct)
    {
        await AvatarModule.StopWalkingAsync(pet, ct);
        await AvatarModule.StopWalkingAsync(rider, ct);

        ClearActionStatuses(pet);
        await LeaveNestAsync(pet, ct);

        pet.Sit(false);
        pet.Lay(false);
        pet.IsFreeRoaming = false;
        pet.FollowObjectId = -1;
        pet.TargetItemId = -1;

        rider.Sit(false);
        rider.Lay(false);

        await AvatarModule.RelocateAvatarAsync(rider, MapModule.ToIdx(pet.X, pet.Y), ct);

        rider.SetRotation(pet.Rotation);
        rider.MarkDirty();

        pet.SetRider(rider.ObjectId);
        MapModule.UpdateHeightForAvatar(rider);

        await AvatarModule.SetAvatarEffectAsync(rider.ObjectId, GetRiderEffectId(pet), ct);
        await BroadcastFigureAsync(pet, ct);
        Persist(pet);
    }

    internal async Task DismountAsync(IRoomPet pet, CancellationToken ct)
    {
        var riderObjectId = pet.RiderObjectId;

        pet.SetRider(-1);
        pet.IsFreeRoaming = true;

        if (AvatarModule.TryGetAvatar(riderObjectId, out var rider))
        {
            await AvatarModule.SetAvatarEffectAsync(rider.ObjectId, 0, ct);

            // Step the rider off the pet so the two do not share a tile.
            foreach (var (x, y) in TilesAround(pet.X, pet.Y))
            {
                var idx = MapModule.ToIdx(x, y);

                if (!IsTileFreeForNpc(idx))
                    continue;

                await AvatarModule.RelocateAvatarAsync(rider, idx, ct);

                break;
            }

            // Down to the floor, also when no tile around was free to step onto.
            MapModule.UpdateHeightForAvatar(rider);
            rider.MarkDirty();
        }

        await BroadcastFigureAsync(pet, ct);
        Persist(pet);
    }

    /// <summary>How far <paramref name="avatar"/> stands above the floor for riding a pet: zero when not riding.</summary>
    internal Altitude GetRiderLift(IRoomAvatar avatar) =>
        IsRiding(avatar) ? Config.RiderHeight : Altitude.Zero;

    /// <summary>Whether <paramref name="avatar"/> sits on a pet's back.</summary>
    internal bool IsRiding(IRoomAvatar avatar)
    {
        // Asked on every step of every avatar, so no allocation.
        for (var i = 0; i < Pets.Count; i++)
        {
            if (Pets[i].RiderObjectId == avatar.ObjectId)
                return true;
        }

        return false;
    }

    /// <summary>
    /// A ridden pet shadows its rider: same tile, facing and movement, on the floor its rider is
    /// lifted above.
    /// </summary>
    internal async Task SyncRidingPetsAsync(CancellationToken ct)
    {
        // Runs on every avatar boundary, mostly with nobody riding, so it walks the list by index
        // instead of filtering it into a new one. Nothing here takes a pet out of the room.
        for (var i = 0; i < Pets.Count; i++)
        {
            var pet = Pets[i];

            if (!pet.IsRiding)
                continue;

            if (!AvatarModule.TryGetAvatar(pet.RiderObjectId, out var rider))
            {
                await DismountAsync(pet, ct);

                continue;
            }

            await AvatarModule.RelocateAvatarAsync(
                pet,
                MapModule.ToIdx(rider.X, rider.Y),
                ct,
                notifyFurni: false
            );

            pet.SetPositionZ(MapModule.GetTileHeightForAvatar(MapModule.ToIdx(rider.X, rider.Y)));
            pet.SetRotation(rider.Rotation);

            if (rider.Statuses.TryGetValue(AvatarStatusType.Move, out var move))
                pet.AddStatus(AvatarStatusType.Move, ToFloorMove(move));
            else
                pet.RemoveStatus(AvatarStatusType.Move);
        }
    }

    /// <summary>The rider's step (<c>x,y,z</c>) as its pet takes it: to the same tile, on the floor.</summary>
    private string ToFloorMove(string riderMove)
    {
        var parts = riderMove.Split(',');

        if (
            parts.Length < 2
            || !int.TryParse(
                parts[0],
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var x
            )
            || !int.TryParse(
                parts[1],
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var y
            )
        )
            return riderMove;

        var floor = MapModule.GetTileHeightForAvatar(MapModule.ToIdx(x, y));

        return $"{x},{y},{floor.ToStatusString()}";
    }

    /// <summary>
    /// The effect a rider of <paramref name="pet"/> wears: the one its saddle names (Habbo's
    /// <c>horse_saddle2</c> gives 103, RidingSaddle), else Riding.
    /// </summary>
    private int GetRiderEffectId(IRoomPet pet) =>
        PetProductParams.ParseSaddle(GetSaddleDefinition(pet)?.CustomParams)?.RiderEffectId
        ?? PetRiding.RIDER_EFFECT_ID;

    /// <summary>
    /// The saddle <paramref name="pet"/> wears; a pet saddled before it kept which saddle it was
    /// wears the configured one.
    /// </summary>
    private FurnitureDefinitionSnapshot? GetSaddleDefinition(IRoomPet pet) =>
        pet.SaddleDefinitionId is { } id
            ? _roomGrain._definitionProvider.TryGetDefinition(id)
            : _roomGrain._definitionProvider.TryGetDefinitionByName(Config.SaddleDefinitionName);

    public async Task<bool> RemovePetSaddleAsync(ActionContext ctx, int petId, CancellationToken ct)
    {
        if (!TryGetPet(petId, out var pet) || pet.OwnerId != ctx.PlayerId || !pet.HasSaddle)
            return false;

        var definition = GetSaddleDefinition(pet);

        if (definition is null)
        {
            Logger.LogError(
                "Saddle definition {Id} ({Name}) is missing; the saddle of pet {PetId} stays on",
                pet.SaddleDefinitionId,
                Config.SaddleDefinitionName,
                petId
            );

            return false;
        }

        if (pet.IsRiding)
            await DismountAsync(pet, ct);

        if (PetProductParams.ParseSaddle(definition.CustomParams) is { } saddle)
            pet.SetPetFigure(
                pet.PetFigure with
                {
                    CustomParts = PetProductParams.WithoutSaddle(pet.PetFigure.CustomParts, saddle),
                }
            );

        await SetSaddleAsync(pet, false, null, ct);
        await PersistNowAsync(ct);

        await _roomGrain
            ._grainFactory.GetInventoryGrain(ctx.PlayerId)
            .GrantFurnitureAsync(
                definition.Id,
                null,
                MovementCause.New(MovementCauses.PET_UNSADDLE, ctx.PlayerId.Value, petId),
                ct
            );

        return true;
    }

    /// <summary>
    /// Straps a saddle on: its parts drawn on the pet (<paramref name="saddle"/>, none when its
    /// definition names none) and the saddle kept to be given back.
    /// </summary>
    internal Task PutSaddleOnAsync(
        IRoomPet pet,
        int saddleDefinitionId,
        PetSaddleParams? saddle,
        CancellationToken ct
    )
    {
        if (saddle is not null)
            pet.SetPetFigure(
                pet.PetFigure with
                {
                    CustomParts = PetProductParams.WithSaddle(pet.PetFigure.CustomParts, saddle),
                }
            );

        return SetSaddleAsync(pet, true, saddleDefinitionId, ct);
    }

    private async Task SetSaddleAsync(
        IRoomPet pet,
        bool hasSaddle,
        int? saddleDefinitionId,
        CancellationToken ct
    )
    {
        pet.SetSaddle(hasSaddle, saddleDefinitionId);

        // A saddled pet stays on its feet, so one saddled lying down gets up.
        if (!CanRest(pet))
        {
            pet.Sit(false);
            pet.Lay(false);
            pet.MarkDirty();
        }

        await BroadcastFigureAsync(pet, ct);
        Persist(pet);
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

        Persist(pet);
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
        Persist(pet);
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
}
