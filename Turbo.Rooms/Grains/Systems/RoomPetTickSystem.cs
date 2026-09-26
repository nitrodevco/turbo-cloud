using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Turbo.Primitives.Pets.Enums;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Rooms.Configuration;

namespace Turbo.Rooms.Grains.Systems;

/// <summary>
/// What pets do on their own between commands: wander, rest, look for food when hungry, follow
/// their owner, and let their stats drift. Monsterplants grow and dry out instead of walking.
/// Runs every room tick; each pet paces itself with its own next-action time.
/// </summary>
public sealed class RoomPetTickSystem(RoomGrain roomGrain)
{
    private readonly RoomGrain _roomGrain = roomGrain;

    private PetConfig Config => _roomGrain._petConfig;

    public async Task ProcessPetsAsync(long now, CancellationToken ct)
    {
        foreach (var pet in _roomGrain.PetModule.Pets.ToList())
        {
            try
            {
                await ProcessPetAsync(pet, now, ct);
            }
            catch (Exception ex)
            {
                _roomGrain._logger.LogError(
                    ex,
                    "Pet {PetId} in room {RoomId} failed to tick",
                    pet.PetId,
                    _roomGrain.RoomId
                );
            }
        }
    }

    private async Task ProcessPetAsync(IRoomPet pet, long now, CancellationToken ct)
    {
        var module = _roomGrain.PetModule;

        if (pet.IsRiding)
            return;

        await module.TryCompletePendingMountAsync(pet, ct);

        if (pet.ActionExpiresAtMs > 0 && now >= pet.ActionExpiresAtMs)
            module.ClearActionStatuses(pet);

        await DecayAsync(pet, now, ct);

        if (pet.IsMonsterplant)
        {
            await ProcessMonsterplantAsync(pet, now, ct);

            return;
        }

        if (pet.IsWalking)
            return;

        await module.PersistPositionIfMovedAsync(pet, ct);

        if (pet.TargetItemId > 0)
        {
            await ArriveAtTargetAsync(pet, ct);

            return;
        }

        if (pet.FollowObjectId > 0)
        {
            await FollowAsync(pet, ct);

            return;
        }

        if (now < pet.NextActionAtMs)
            return;

        pet.NextActionAtMs =
            now + module.NextRandom(Config.IdleActionMinMs, Config.IdleActionMaxMs);

        if (!pet.IsFreeRoaming)
        {
            if (pet.HasStatus(AvatarStatusType.Lay))
                pet.SetEnergy(Math.Min(Config.MaxEnergy, pet.Energy + Config.RestEnergyPerTick));

            return;
        }

        if (
            pet.Nutrition < Config.HungryNutrition
            && await module.WalkToSupplyAsync(pet, PetSupplyType.Food, ct)
        )
            return;

        if (
            pet.Energy < Config.TiredEnergy
            && await module.WalkToSupplyAsync(pet, PetSupplyType.Nest, ct)
        )
            return;

        if (
            pet.Energy < Config.TiredEnergy
            && await module.WalkToSupplyAsync(pet, PetSupplyType.Drink, ct)
        )
            return;

        if (module.Chance(Config.FreeRoamWalkChancePercent))
        {
            await WanderAsync(pet, ct);

            return;
        }

        await IdleAsync(pet, ct);
    }

    private async Task DecayAsync(IRoomPet pet, long now, CancellationToken ct)
    {
        var changed = false;

        if (pet.NextEnergyDecayAtMs == 0)
            pet.NextEnergyDecayAtMs = now + Config.EnergyDecayMs;
        else if (now >= pet.NextEnergyDecayAtMs)
        {
            pet.NextEnergyDecayAtMs = now + Config.EnergyDecayMs;

            if (!pet.HasStatus(AvatarStatusType.Lay) && pet.Energy > 0)
            {
                pet.SetEnergy(pet.Energy - 1);
                changed = true;
            }
        }

        if (pet.NextNutritionDecayAtMs == 0)
            pet.NextNutritionDecayAtMs = now + Config.NutritionDecayMs;
        else if (now >= pet.NextNutritionDecayAtMs)
        {
            pet.NextNutritionDecayAtMs = now + Config.NutritionDecayMs;

            if (pet.Nutrition > 0)
            {
                pet.SetNutrition(pet.Nutrition - 1);
                changed = true;
            }
        }

        if (changed)
            await _roomGrain.PetModule.PersistAsync(pet, ct);
    }

    private async Task ProcessMonsterplantAsync(IRoomPet pet, long now, CancellationToken ct)
    {
        var module = _roomGrain.PetModule;

        if (now < pet.NextActionAtMs)
            return;

        pet.NextActionAtMs = now + Config.IdleActionMaxMs;

        if (
            pet.Level < Config.MonsterplantMaxLevel
            && module.RemainingWellBeingSeconds(pet) > 0
            && module.RemainingGrowingSeconds(pet) == 0
        )
        {
            await module.LevelUpAsync(pet, ct);
            await module.PersistAsync(pet, ct);

            return;
        }

        var (canBreed, canHarvest, canRevive) = (pet.CanBreed, pet.CanHarvest, pet.CanRevive);

        module.RefreshFlags(pet);

        if (canBreed != pet.CanBreed || canHarvest != pet.CanHarvest || canRevive != pet.CanRevive)
            await module.BroadcastStatusAsync(pet, ct);
    }

    private async Task ArriveAtTargetAsync(IRoomPet pet, CancellationToken ct)
    {
        if (!_roomGrain._state.ItemsById.TryGetValue(pet.TargetItemId, out var item))
        {
            pet.TargetItemId = -1;

            return;
        }

        if (pet.X == item.X && pet.Y == item.Y)
        {
            await _roomGrain.PetModule.OnPetReachedItemAsync(pet, ct);

            return;
        }

        // The walk ended short of the item (blocked or unreachable); give up on it.
        pet.TargetItemId = -1;
    }

    private async Task FollowAsync(IRoomPet pet, CancellationToken ct)
    {
        if (!_roomGrain.AvatarModule.TryGetAvatar(pet.FollowObjectId, out var target))
        {
            pet.FollowObjectId = -1;

            return;
        }

        if (Modules.RoomAvatarModule.AreAdjacent(pet, target))
        {
            if (!pet.IsWalking && !target.IsWalking)
                pet.SetBodyRotation(target.Rotation);

            return;
        }

        await _roomGrain.PetModule.WalkNextToAsync(pet, target, ct);
    }

    private async Task WanderAsync(IRoomPet pet, CancellationToken ct)
    {
        var module = _roomGrain.PetModule;
        var map = _roomGrain.MapModule;
        var range = Config.FreeRoamMaxDistance;

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var x = pet.X + module.NextRandom(-range, range + 1);
            var y = pet.Y + module.NextRandom(-range, range + 1);

            if (!map.InBounds(x, y) || (x == pet.X && y == pet.Y))
                continue;

            if (!module.IsTileFreeForNpc(map.ToIdx(x, y)))
                continue;

            pet.Sit(false);
            pet.Lay(false);

            if (await _roomGrain.AvatarModule.WalkAvatarToAsync(pet, x, y, ct))
                return;
        }
    }

    private async Task IdleAsync(IRoomPet pet, CancellationToken ct)
    {
        var module = _roomGrain.PetModule;

        switch (module.NextRandom(0, 6))
        {
            case 0:
                pet.Sit(true);
                break;
            case 1:
                pet.Lay(true);
                break;
            case 2:
                pet.Sit(false);
                pet.Lay(false);
                pet.AddStatus(AvatarStatusType.WagTail, string.Empty);
                pet.ActionExpiresAtMs = _roomGrain.NowMs() + Config.ActionDurationMs;
                break;
            case 3:
                await module.SpeakAsync(pet, ct);
                break;
            default:
                pet.Sit(false);
                pet.Lay(false);
                pet.SetBodyRotation((Rotation)module.NextRandom(0, 8));
                break;
        }
    }
}
