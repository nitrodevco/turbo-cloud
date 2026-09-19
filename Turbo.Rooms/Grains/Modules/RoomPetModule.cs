using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Outgoing.Room.Pets;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Pets;
using Turbo.Primitives.Pets.Enums;
using Turbo.Primitives.Pets.Snapshots;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Rooms.Configuration;

namespace Turbo.Rooms.Grains.Modules;

/// <summary>
/// Pets standing in the room: placement and pick-up against the owner's inventory, the info
/// stand, and the stats the room mutates while the pet is here. Behaviour over time runs in
/// <see cref="Systems.RoomPetTickSystem"/>.
/// </summary>
public sealed partial class RoomPetModule(RoomGrain roomGrain)
{
    private readonly RoomGrain _roomGrain = roomGrain;
    private readonly Dictionary<int, int> _lastPersistedTileByPetId = [];
    private readonly Random _random = new();

    private PetConfig Config => _roomGrain._petConfig;

    public IEnumerable<IRoomPet> Pets =>
        _roomGrain._state.AvatarsByObjectId.Values.OfType<IRoomPet>();

    internal async Task EnsurePetsLoadedAsync(CancellationToken ct)
    {
        if (_roomGrain._state.IsPetsLoaded)
            return;

        var pets = await _roomGrain._npcProvider.LoadPetsByRoomIdAsync(_roomGrain.RoomId, ct);

        foreach (var pet in pets)
        {
            var tileIdx = _roomGrain.MapModule.InBounds(pet.X, pet.Y)
                ? _roomGrain.MapModule.ToIdx(pet.X, pet.Y)
                : -1;

            if (tileIdx < 0 && !TryFindFreeTile(0, 0, out tileIdx))
            {
                _roomGrain._logger.LogWarning(
                    "Pet {PetId} has no tile to stand on in room {RoomId}; leaving it unplaced",
                    pet.Id,
                    _roomGrain.RoomId
                );

                continue;
            }

            await AttachPetAsync(pet, tileIdx, pet.Rotation, ct);
        }

        _roomGrain._state.IsPetsLoaded = true;
    }

    public bool TryGetPet(int petId, out IRoomPet pet)
    {
        pet = null!;

        if (
            !_roomGrain._state.AvatarsByPetId.TryGetValue(petId, out var objectId)
            || !_roomGrain._state.AvatarsByObjectId.TryGetValue(objectId, out var avatar)
            || avatar is not IRoomPet roomPet
        )
            return false;

        pet = roomPet;

        return true;
    }

    private Task SendPlacingErrorAsync(
        ActionContext ctx,
        PetPlacingErrorType error,
        CancellationToken ct
    ) =>
        _roomGrain._grainFactory.SendComposerToPlayerAsync(
            ctx.PlayerId,
            new PetPlacingErrorMessageComposer { Error = error },
            ct
        );

    /// <summary>The pet's owner, or the room owner, may manage a placed pet.</summary>
    internal async Task<bool> CanManageAsync(ActionContext ctx, IRoomPet pet) =>
        pet.OwnerId == ctx.PlayerId || await _roomGrain.SecurityModule.GetIsRoomOwnerAsync(ctx);

    public async Task<bool> PlacePetAsync(
        ActionContext ctx,
        int petId,
        int x,
        int y,
        CancellationToken ct
    )
    {
        if (!_roomGrain.AvatarModule.TryGetPlayer(ctx.PlayerId, out _))
            return false;

        var room = _roomGrain._state.RoomSnapshot;

        if (!room.AllowPets && !await _roomGrain.SecurityModule.GetIsRoomOwnerAsync(ctx))
        {
            await SendPlacingErrorAsync(ctx, PetPlacingErrorType.ForbiddenInFlat, ct);

            return false;
        }

        if (Pets.Count() >= Config.MaxPetsPerRoom)
        {
            await SendPlacingErrorAsync(ctx, PetPlacingErrorType.MaxPetsInRoom, ct);

            return false;
        }

        // The inventory's place button sends (0, 0) when no tile was chosen.
        var chosen = x != 0 || y != 0;

        if (!TryFindFreeTile(x, y, out var tileIdx, chosen))
        {
            await SendPlacingErrorAsync(
                ctx,
                chosen ? PetPlacingErrorType.SelectedTileNotFree : PetPlacingErrorType.NoFreeTiles,
                ct
            );

            return false;
        }

        var snapshot = await _roomGrain
            ._grainFactory.GetInventoryGrain(ctx.PlayerId)
            .TryCheckOutPetAsync(petId, _roomGrain.RoomId, ct);

        if (snapshot is null)
        {
            _roomGrain._logger.LogWarning(
                "Player {PlayerId} tried to place pet {PetId} they do not hold in room {RoomId}",
                ctx.PlayerId,
                petId,
                _roomGrain.RoomId
            );

            return false;
        }

        // The tile may have been taken while the inventory was consulted.
        if (!IsTileFreeForNpc(tileIdx) && !TryFindFreeTile(0, 0, out tileIdx))
        {
            await _roomGrain
                ._grainFactory.GetInventoryGrain(ctx.PlayerId)
                .ReturnPetAsync(snapshot, ct);
            await SendPlacingErrorAsync(ctx, PetPlacingErrorType.NoFreeTiles, ct);

            return false;
        }

        var (tileX, tileY) = _roomGrain.MapModule.GetTileXY(tileIdx);
        var placed = snapshot with
        {
            RoomId = _roomGrain.RoomId,
            X = tileX,
            Y = tileY,
            Z = _roomGrain._state.TileHeights[tileIdx],
        };

        if (!await AttachPetAsync(placed, tileIdx, placed.Rotation, ct))
        {
            await _roomGrain
                ._grainFactory.GetInventoryGrain(ctx.PlayerId)
                .ReturnPetAsync(snapshot, ct);

            return false;
        }

        if (TryGetPet(petId, out var pet))
            await PersistAsync(pet, ct);

        return true;
    }

    /// <summary>Only monsterplants are moved by hand; every other pet walks on its own.</summary>
    public async Task<bool> MovePetAsync(
        ActionContext ctx,
        int petId,
        int x,
        int y,
        Rotation rotation,
        CancellationToken ct
    )
    {
        if (!TryGetPet(petId, out var pet) || !pet.IsMonsterplant)
            return false;

        if (!await CanManageAsync(ctx, pet))
            return false;

        if (!_roomGrain.MapModule.InBounds(x, y))
            return false;

        var tileIdx = _roomGrain.MapModule.ToIdx(x, y);
        var currentIdx = _roomGrain.MapModule.ToIdx(pet.X, pet.Y);

        if (tileIdx != currentIdx)
        {
            if (!IsTileFreeForNpc(tileIdx))
                return false;

            _roomGrain.MapModule.RemoveAvatar(pet, false);

            pet.SetPosition(x, y);

            _roomGrain.MapModule.AddAvatar(pet, false);
            _roomGrain.MapModule.UpdateHeightForAvatar(pet);
        }

        if (rotation != Rotation.None)
            pet.SetRotation(rotation);

        pet.MarkDirty();

        await PersistAsync(pet, ct);

        return true;
    }

    public async Task<bool> PickupPetAsync(ActionContext ctx, int petId, CancellationToken ct)
    {
        if (!TryGetPet(petId, out var pet))
            return false;

        if (!await CanManageAsync(ctx, pet))
            return false;

        await RemovePetAvatarAsync(ctx, pet, ct);

        var snapshot = pet.GetPetSnapshot();

        if (
            !await _roomGrain
                ._grainFactory.GetInventoryGrain(pet.OwnerId)
                .ReturnPetAsync(snapshot, ct)
        )
        {
            _roomGrain._logger.LogError(
                "Pet {PetId} picked up from room {RoomId} could not be returned to player {OwnerId}",
                petId,
                _roomGrain.RoomId,
                pet.OwnerId
            );
        }

        return true;
    }

    private async Task<bool> AttachPetAsync(
        PetSnapshot snapshot,
        int tileIdx,
        Rotation rotation,
        CancellationToken ct
    )
    {
        var objectId = _roomGrain.AvatarModule.GetNextObjectId();
        var pet = _roomGrain._avatarProvider.CreateAvatarFromPetSnapshot(objectId, snapshot);

        pet.NextTileId = tileIdx;

        RefreshFlags(pet);
        RefreshPosture(pet);

        if (!await _roomGrain.ObjectModule.AttatchObjectAsync(pet, ct))
            return false;

        pet.SetRotation(rotation == Rotation.None ? Rotation.South : rotation);

        _roomGrain._state.AvatarsByPetId[pet.PetId] = pet.ObjectId;
        _lastPersistedTileByPetId[pet.PetId] = tileIdx;

        return true;
    }

    internal async Task RemovePetAvatarAsync(ActionContext ctx, IRoomPet pet, CancellationToken ct)
    {
        if (pet.IsRiding)
            await DismountAsync(pet, ct);

        await LeaveNestAsync(pet, ct);
        await CancelPlantBreedingsInvolvingAsync(pet.PetId, ct);

        await _roomGrain.ObjectModule.RemoveObjectAsync(ctx, pet, ct);

        _roomGrain._state.AvatarsByPetId.Remove(pet.PetId);
        _lastPersistedTileByPetId.Remove(pet.PetId);
    }

    /// <summary>
    /// Returns every pet to its owner's inventory, as a room deletion requires. Same shape as
    /// <see cref="RoomBotModule.ReturnAllToOwnersAsync"/>.
    /// </summary>
    internal async Task ReturnAllToOwnersAsync(CancellationToken ct)
    {
        var pets = Pets.ToList();

        foreach (var pet in pets)
            await RemovePetAvatarAsync(ActionContext.CreateForSystem(_roomGrain.RoomId), pet, ct);

        await Task.WhenAll(
            pets.GroupBy(x => x.OwnerId).Select(owned => ReturnToOwnerAsync(owned.Key, owned, ct))
        );
    }

    private async Task ReturnToOwnerAsync(
        PlayerId ownerId,
        IEnumerable<IRoomPet> pets,
        CancellationToken ct
    )
    {
        var inventory = _roomGrain._grainFactory.GetInventoryGrain(ownerId);

        foreach (var pet in pets)
        {
            if (!await inventory.ReturnPetAsync(pet.GetPetSnapshot(), ct))
                _roomGrain._logger.LogError(
                    "Pet {PetId} could not be returned to player {OwnerId} while room {RoomId} is deleted",
                    pet.PetId,
                    ownerId,
                    _roomGrain.RoomId
                );
        }
    }

    /// <summary>
    /// A tile a pet or bot can stand on. With <paramref name="exact"/> only the given tile is
    /// considered; otherwise the search spirals out from it (the door when nothing was chosen).
    /// </summary>
    internal bool TryFindFreeTile(int x, int y, out int tileIdx, bool exact = false)
    {
        tileIdx = -1;

        var map = _roomGrain.MapModule;

        if (exact)
        {
            if (!map.InBounds(x, y) || !IsTileFreeForNpc(map.ToIdx(x, y)))
                return false;

            tileIdx = map.ToIdx(x, y);

            return true;
        }

        var model = _roomGrain._state.Model;
        var startX = model?.DoorX ?? 0;
        var startY = model?.DoorY ?? 0;
        var maxRadius = Math.Max(map.Width, map.Height);

        for (var radius = 0; radius <= maxRadius; radius++)
        {
            for (var dx = -radius; dx <= radius; dx++)
            {
                for (var dy = -radius; dy <= radius; dy++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != radius)
                        continue;

                    var candidateX = startX + dx;
                    var candidateY = startY + dy;

                    if (!map.InBounds(candidateX, candidateY))
                        continue;

                    var idx = map.ToIdx(candidateX, candidateY);

                    // The door tile itself is left free for arriving players.
                    if (radius == 0 && model is not null)
                        continue;

                    if (!IsTileFreeForNpc(idx))
                        continue;

                    tileIdx = idx;

                    return true;
                }
            }
        }

        return false;
    }

    internal bool IsTileFreeForNpc(int tileIdx)
    {
        if (!_roomGrain.MapModule.InBounds(tileIdx))
            return false;

        var flags = _roomGrain._state.TileFlags[tileIdx];

        if (flags.Has(RoomTileFlags.Disabled) || flags.Has(RoomTileFlags.Closed))
            return false;

        if (flags.Has(RoomTileFlags.AvatarOccupied))
            return false;

        return !flags.Has(RoomTileFlags.FurnitureOccupied) || flags.Has(RoomTileFlags.Walkable);
    }

    /// <summary>The context-menu flags the client keys its buttons on.</summary>
    internal void RefreshFlags(IRoomPet pet)
    {
        if (!pet.IsMonsterplant)
        {
            pet.SetFlags(
                canBreed: PetTypes.CanNestBreed(pet.TypeId)
                    && pet.Level >= Config.NestBreedingMinLevel,
                canHarvest: false,
                canRevive: false
            );

            return;
        }

        var grown = pet.Level >= Config.MonsterplantMaxLevel;
        var dead = RemainingWellBeingSeconds(pet) <= 0;
        var harvestable =
            grown
            && !dead
            && (
                pet.HarvestedAtUtc is null
                || (DateTime.UtcNow - pet.HarvestedAtUtc.Value).TotalSeconds
                    >= Config.MonsterplantHarvestIntervalSeconds
            );

        pet.SetFlags(canBreed: grown && !dead, canHarvest: harvestable, canRevive: dead);
    }

    internal void RefreshPosture(IRoomPet pet)
    {
        if (pet.IsMonsterplant)
            pet.SetPosture(PetPostures.ForMonsterplant(pet.Level));
    }

    internal Task PersistAsync(IRoomPet pet, CancellationToken ct)
    {
        _lastPersistedTileByPetId[pet.PetId] = _roomGrain.MapModule.ToIdx(pet.X, pet.Y);

        return _roomGrain
            ._grainFactory.GetRoomPersistenceGrain(_roomGrain.RoomId)
            .EnqueueDirtyPetAsync(pet.GetPetSnapshot(), ct);
    }

    /// <summary>Writes the position once a pet has come to rest somewhere new.</summary>
    internal Task PersistPositionIfMovedAsync(IRoomPet pet, CancellationToken ct)
    {
        if (pet.IsWalking || pet.IsRiding)
            return Task.CompletedTask;

        var tileIdx = _roomGrain.MapModule.ToIdx(pet.X, pet.Y);

        if (_lastPersistedTileByPetId.TryGetValue(pet.PetId, out var last) && last == tileIdx)
            return Task.CompletedTask;

        return PersistAsync(pet, ct);
    }

    public Task<bool> SelectPetAsync(ActionContext ctx, int petId, CancellationToken ct)
    {
        if (!TryGetPet(petId, out _))
            return Task.FromResult(false);

        _roomGrain.AvatarModule.TouchAvatar(ctx.PlayerId, _roomGrain.NowMs());

        return Task.FromResult(true);
    }

    public async Task<bool> RequestPetInfoAsync(ActionContext ctx, int petId, CancellationToken ct)
    {
        if (!TryGetPet(petId, out var pet))
            return false;

        RefreshFlags(pet);

        await _roomGrain._grainFactory.SendComposerToPlayerAsync(
            ctx.PlayerId,
            new PetInfoMessageComposer { Info = BuildInfo(pet) },
            ct
        );

        return true;
    }

    internal Task SendInfoToOwnerAsync(IRoomPet pet, CancellationToken ct) =>
        _roomGrain._grainFactory.SendComposerToPlayerAsync(
            pet.OwnerId,
            new PetInfoMessageComposer { Info = BuildInfo(pet) },
            ct
        );

    internal Task BroadcastStatusAsync(IRoomPet pet, CancellationToken ct) =>
        _roomGrain.SendComposerToRoomAsync(
            new PetStatusUpdateMessageComposer
            {
                ObjectId = pet.ObjectId,
                PetId = pet.PetId,
                CanBreed = pet.CanBreed,
                CanHarvest = pet.CanHarvest,
                CanRevive = pet.CanRevive,
                HasBreedingPermission = pet.HasBreedingPermission,
            },
            ct
        );

    internal PetInfoSnapshot BuildInfo(IRoomPet pet) =>
        new()
        {
            PetId = pet.PetId,
            Name = pet.Name,
            Level = pet.Level,
            MaxLevel = MaxLevelFor(pet),
            Experience = pet.Experience,
            ExperienceRequiredToLevel = ExperienceToLevel(pet.Level),
            Energy = pet.Energy,
            MaxEnergy = Config.MaxEnergy,
            Nutrition = pet.Nutrition,
            MaxNutrition = Config.MaxNutrition,
            Respect = pet.Respect,
            OwnerId = pet.OwnerId,
            AgeDays = Math.Max(0, (int)(DateTime.UtcNow - pet.CreatedAtUtc).TotalDays),
            OwnerName = pet.OwnerName,
            BreedId = pet.PetFigure.BreedId,
            HasFreeSaddle = pet.HasSaddle,
            IsRiding = pet.IsRiding,
            SkillThresholds = [.. Config.SkillThresholdLevels.OrderBy(x => x)],
            AccessRights = pet.AnyoneCanRide ? PetRiding.ACCESS_ANYONE : PetRiding.ACCESS_OWNER,
            CanBreed = pet.CanBreed,
            CanHarvest = pet.CanHarvest,
            CanRevive = pet.CanRevive,
            RarityLevel = pet.RarityLevel,
            MaxWellBeingSeconds = pet.IsMonsterplant ? Config.MonsterplantWellBeingSeconds : 0,
            RemainingWellBeingSeconds = pet.IsMonsterplant ? RemainingWellBeingSeconds(pet) : 0,
            RemainingGrowingSeconds = pet.IsMonsterplant ? RemainingGrowingSeconds(pet) : 0,
            HasBreedingPermission = pet.HasBreedingPermission,
        };

    internal int MaxLevelFor(IRoomPet pet) =>
        pet.IsMonsterplant ? Config.MonsterplantMaxLevel : Config.MaxLevel;

    /// <summary>Experience needed to leave <paramref name="level"/>; the top level never fills.</summary>
    internal int ExperienceToLevel(int level)
    {
        var thresholds = Config.LevelExperienceThresholds;

        if (thresholds.Length == 0)
            return int.MaxValue;

        var index = Math.Clamp(level - 1, 0, thresholds.Length - 1);

        return thresholds[index];
    }

    internal int RemainingWellBeingSeconds(IRoomPet pet)
    {
        var elapsed = (DateTime.UtcNow - pet.WateredAtUtc).TotalSeconds;

        return (int)Math.Max(0, Config.MonsterplantWellBeingSeconds - elapsed);
    }

    internal int RemainingGrowingSeconds(IRoomPet pet)
    {
        if (pet.Level >= Config.MonsterplantMaxLevel)
            return 0;

        var nextLevelAt = pet.CreatedAtUtc.AddSeconds(
            (double)pet.Level * Config.MonsterplantGrowthSeconds
        );

        return (int)Math.Max(0, (nextLevelAt - DateTime.UtcNow).TotalSeconds);
    }

    internal int NextRandom(int minInclusive, int maxExclusive) =>
        _random.Next(minInclusive, Math.Max(minInclusive + 1, maxExclusive));

    internal bool Chance(int percent) => _random.Next(100) < percent;
}
