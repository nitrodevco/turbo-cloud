using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Turbo.Primitives.Pets.Enums;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Rooms.Configuration;
using Turbo.Rooms.Grains.Modules;

namespace Turbo.Rooms.Grains.Systems;

/// <summary>
/// What pets do on their own between commands: wander, rest, look for food when hungry, follow
/// their owner, and let their stats drift. Monsterplants grow and dry out instead of walking.
/// Runs on every avatar boundary, the only moment a walk moves; each pet paces itself with its
/// own next-action time.
/// </summary>
public sealed class RoomPetTickSystem(RoomGrain roomGrain) : RoomGrainComponent(roomGrain)
{
    // A pet can leave the room during its own turn (a breeding, a compost), so the loop walks a
    // copy; the list is kept rather than made again every boundary.
    private readonly List<IRoomPet> _pets = [];

    private PetConfig Config => _roomGrain._petConfig;

    /// <summary>Statuses by the code the client knows them by, for the configured idle actions.</summary>
    private static readonly Dictionary<string, AvatarStatusType> STATUS_BY_CODE =
        Enum.GetValues<AvatarStatusType>()
            .ToDictionary(x => x.ToLegacyString(), StringComparer.OrdinalIgnoreCase);

    // Decay runs on wall-clock time; read once per pass so every pet in it agrees.
    private DateTime _utcNow;

    public async Task ProcessPetsAsync(long now, CancellationToken ct)
    {
        _utcNow = DateTime.UtcNow;
        _pets.Clear();
        _pets.AddRange(PetModule.Pets);

        foreach (var pet in _pets)
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
        var module = PetModule;

        if (pet.IsRiding)
            return;

        await module.TryCompletePendingMountAsync(pet, ct);

        // Someone is walking over to ride it: it waits for them where it stands.
        if (pet.PendingRiderObjectId > 0)
            return;

        if (pet.ActionExpiresAtMs > 0 && now >= pet.ActionExpiresAtMs)
            module.ClearActionStatuses(pet);

        Decay(pet);

        if (pet.IsMonsterplant)
        {
            await ProcessMonsterplantAsync(pet, now, ct);

            return;
        }

        // Where a pet came to rest is written with the room's next hand-over to persistence.
        if (pet.IsWalking)
            return;

        if (pet.TargetItemId > 0)
        {
            await ArriveAtTargetAsync(pet, ct);

            return;
        }

        if (pet.FollowObjectId > 0)
        {
            await FollowAsync(pet, now, ct);

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
            && module.CanRest(pet)
            && await module.WalkToSupplyAsync(pet, PetSupplyType.Nest, ct)
        )
            return;

        if (
            pet.Energy < Config.TiredEnergy
            && await module.WalkToSupplyAsync(pet, PetSupplyType.Drink, ct)
        )
            return;

        // Official pets go and play with a toy in the room on their own now and then.
        if (
            module.Chance(Config.ToyPlayChancePercent)
            && await module.WalkToSupplyAsync(pet, PetSupplyType.Toy, ct)
        )
            return;

        if (module.Chance(Config.FreeRoamWalkChancePercent))
        {
            await WanderAsync(pet, ct);

            return;
        }

        await IdleAsync(pet, ct);
    }

    /// <summary>
    /// Takes every point of energy and nutrition that has fallen due, however long ago. The due
    /// times are wall-clock and persisted with the pet, so the time a room spent unloaded or
    /// dormant is caught up on the pet's first tick back rather than lost. A resting pet does not
    /// lose energy; for a catch-up that is judged by how it lies now.
    /// </summary>
    private void Decay(IRoomPet pet)
    {
        var energyDue = pet.EnergyDecayDueUtc;
        var nutritionDue = pet.NutritionDecayDueUtc;

        var energyLost = TakeElapsedPeriods(ref energyDue, Config.EnergyDecayMs);
        var nutritionLost = TakeElapsedPeriods(ref nutritionDue, Config.NutritionDecayMs);

        if (energyDue == pet.EnergyDecayDueUtc && nutritionDue == pet.NutritionDecayDueUtc)
            return;

        pet.EnergyDecayDueUtc = energyDue;
        pet.NutritionDecayDueUtc = nutritionDue;

        if (energyLost > 0 && !pet.HasStatus(AvatarStatusType.Lay))
            pet.SetEnergy(Math.Max(0, pet.Energy - energyLost));

        if (nutritionLost > 0)
            pet.SetNutrition(Math.Max(0, pet.Nutrition - nutritionLost));

        // Written whenever a due time moves, not only when a stat did: a due time left behind in
        // the database would be decayed again after the next load.
        PetModule.Persist(pet);
    }

    /// <summary>
    /// How many whole periods have passed since <paramref name="due"/>, moving it past now. A
    /// pet with no due time yet starts its clock here and loses nothing.
    /// </summary>
    private int TakeElapsedPeriods(ref DateTime? due, int periodMs)
    {
        var period = TimeSpan.FromMilliseconds(Math.Max(1, periodMs));

        if (due is not { } dueAt)
        {
            due = _utcNow + period;

            return 0;
        }

        if (_utcNow < dueAt)
            return 0;

        var periods = (_utcNow - dueAt).Ticks / period.Ticks + 1;

        due = dueAt + TimeSpan.FromTicks(period.Ticks * periods);

        return (int)Math.Min(periods, int.MaxValue);
    }

    private async Task ProcessMonsterplantAsync(IRoomPet pet, long now, CancellationToken ct)
    {
        var module = PetModule;

        if (now < pet.NextActionAtMs)
            return;

        pet.NextActionAtMs = now + Config.IdleActionMaxMs;

        if (
            pet.Level < Config.MonsterplantMaxLevel
            && module.RemainingWellBeingSeconds(pet) > 0
            && module.SecondsToNextLevel(pet) == 0
        )
        {
            await module.LevelUpAsync(pet, ct);
            module.Persist(pet);

            return;
        }

        var (canBreed, canHarvest, canRevive) = (pet.CanBreed, pet.CanHarvest, pet.CanRevive);

        module.RefreshFlags(pet);

        if (canBreed != pet.CanBreed || canHarvest != pet.CanHarvest || canRevive != pet.CanRevive)
            await module.BroadcastStatusAsync(pet, ct);
    }

    private async Task ArriveAtTargetAsync(IRoomPet pet, CancellationToken ct)
    {
        if (!FurniModule.TryGetItem(pet.TargetItemId, out var item))
        {
            pet.TargetItemId = -1;

            return;
        }

        if (pet.X == item.X && pet.Y == item.Y)
        {
            await PetModule.OnPetReachedItemAsync(pet, ct);

            return;
        }

        // The walk ended short of the item (blocked or unreachable); give up on it.
        pet.TargetItemId = -1;
    }

    private async Task FollowAsync(IRoomPet pet, long now, CancellationToken ct)
    {
        if (!AvatarModule.TryGetAvatar(pet.FollowObjectId, out var target))
        {
            pet.FollowObjectId = -1;

            return;
        }

        if (RoomAvatarModule.AreAdjacent(pet, target))
        {
            // The whole pet turns: a body turned alone would leave the head looking elsewhere.
            if (!pet.IsWalking && !target.IsWalking)
                pet.SetRotation(target.Rotation);

            return;
        }

        var targetIdx = MapModule.ToIdx(target.X, target.Y);

        if (!RoomAvatarModule.IsFollowDue(pet, targetIdx, now))
            return;

        var found = await PetModule.WalkNextToAsync(pet, target, ct);

        AvatarModule.RecordFollow(pet, targetIdx, found, now);
    }

    private async Task WanderAsync(IRoomPet pet, CancellationToken ct)
    {
        var module = PetModule;
        var map = MapModule;
        var range = Config.FreeRoamMaxDistance;

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var x = pet.X + module.NextRandom(-range, range + 1);
            var y = pet.Y + module.NextRandom(-range, range + 1);

            if (!map.InBounds(x, y) || (x == pet.X && y == pet.Y))
                continue;

            if (!module.IsTileFreeForPetWalk(pet, map.ToIdx(x, y)))
                continue;

            pet.Sit(false);
            pet.Lay(false);

            if (await AvatarModule.WalkAvatarToAsync(pet, x, y, ct))
                return;
        }
    }

    private async Task IdleAsync(IRoomPet pet, CancellationToken ct)
    {
        var module = PetModule;

        if (Config.IdleActionsByType.TryGetValue(pet.TypeId, out var actions) && actions.Length > 0)
        {
            ShowIdleAction(pet, PickWeighted(actions));

            return;
        }

        switch (module.NextRandom(0, 6))
        {
            case 0 when module.CanRest(pet) && !module.TryGetSeat(pet, out _):
                pet.Sit(true);
                break;
            case 1 when module.CanRest(pet):
                pet.Lay(true, module.TryGetSeat(pet, out _) ? Config.PetSeatHeight : null);
                break;
            case 2:
                module.StandOrStaySeated(pet);
                pet.AddStatus(AvatarStatusType.WagTail, string.Empty);
                pet.ActionExpiresAtMs = _roomGrain.NowMs() + Config.ActionDurationMs;
                break;
            case 3:
                await module.SpeakAsync(pet, ct);
                break;
            default:
                module.StandOrStaySeated(pet);
                // Looking round turns the whole pet, head with body, as an avatar turns.
                pet.SetRotation((Rotation)module.NextRandom(0, 8));
                break;
        }
    }

    /// <summary>
    /// One of the actions, each as likely as its ":&lt;weight&gt;" suffix says (1 without one);
    /// the action is returned without its weight.
    /// </summary>
    private string PickWeighted(string[] actions)
    {
        var total = 0;

        foreach (var action in actions)
            total += Weight(action).Weight;

        var roll = PetModule.NextRandom(0, Math.Max(1, total));

        foreach (var action in actions)
        {
            var (name, weight) = Weight(action);

            if (roll < weight)
                return name;

            roll -= weight;
        }

        return Weight(actions[^1]).Name;

        static (string Name, int Weight) Weight(string action)
        {
            var colon = action.LastIndexOf(':');

            return colon > 0 && int.TryParse(action[(colon + 1)..], out var weight)
                ? (action[..colon], Math.Max(0, weight))
                : (action, 1);
        }
    }

    /// <summary>
    /// One of the type's own idle actions: "std" to just stand, "sit" / "lay" to rest (a saddled
    /// pet stays standing), otherwise a posture or "gst &lt;gesture&gt;" shown for a moment. A
    /// pet on a seat stays seated through all of them and lies down at its seat height there.
    /// </summary>
    private void ShowIdleAction(IRoomPet pet, string action)
    {
        var parts = action.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length == 0)
            return;

        var standOnly = parts[0].Equals("std", StringComparison.OrdinalIgnoreCase);

        if (!standOnly && !STATUS_BY_CODE.TryGetValue(parts[0], out _))
            return;

        PetModule.ClearActionStatuses(pet);
        PetModule.StandOrStaySeated(pet);

        if (standOnly)
            return;

        var status = STATUS_BY_CODE[parts[0]];

        if (status is AvatarStatusType.Sit or AvatarStatusType.Lay)
        {
            if (!PetModule.CanRest(pet))
                return;

            var onSeat = PetModule.TryGetSeat(pet, out _);

            if (status == AvatarStatusType.Lay)
                pet.Lay(true, onSeat ? Config.PetSeatHeight : null);
            else if (!onSeat)
                pet.Sit(true);

            return;
        }

        pet.AddStatus(status, parts.Length > 1 ? parts[1] : string.Empty);
        pet.ActionExpiresAtMs = _roomGrain.NowMs() + Config.IdlePostureDurationMs;
    }
}
