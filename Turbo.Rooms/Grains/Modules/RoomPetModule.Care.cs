using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Outgoing.Room.Pets;
using Turbo.Primitives.Messages.Outgoing.Users;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Pets;
using Turbo.Primitives.Pets.Enums;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Pets;

namespace Turbo.Rooms.Grains.Modules;

public sealed partial class RoomPetModule
{
    public async Task<bool> RespectPetAsync(ActionContext ctx, int petId, CancellationToken ct)
    {
        if (
            !TryGetPet(petId, out var pet)
            || !_roomGrain.AvatarModule.TryGetPlayer(ctx.PlayerId, out _)
        )
            return false;

        var playerGrain = _roomGrain._grainFactory.GetPlayerGrain(ctx.PlayerId);

        if (Config.RespectMinAccountAgeDays > 0)
        {
            var summary = await playerGrain.GetSummaryAsync(ct);
            var ageDays = (int)(DateTime.UtcNow - summary.CreatedAt).TotalDays;

            if (ageDays < Config.RespectMinAccountAgeDays)
            {
                await _roomGrain._grainFactory.SendComposerToPlayerAsync(
                    ctx.PlayerId,
                    new PetRespectFailedMessageComposer
                    {
                        RequiredDays = Config.RespectMinAccountAgeDays,
                        AvatarAgeDays = ageDays,
                    },
                    ct
                );

                return false;
            }
        }

        if (!await playerGrain.TryUsePetRespectAsync(ct))
            return false;

        pet.SetRespect(pet.Respect + 1);

        if (pet.IsMonsterplant)
        {
            // "Treat" in the plant menu is a scratch; it also perks the plant up.
            pet.SetEnergy(
                Math.Min(Config.MaxEnergy, pet.Energy + Config.MonsterplantSupplementEnergy)
            );
        }
        else if (!pet.IsRiding)
        {
            pet.AddStatus(AvatarStatusType.Gesture, PetGestures.SMILE);
            pet.ActionExpiresAtMs = _roomGrain.NowMs() + Config.ActionDurationMs;
        }

        await _roomGrain.SendComposerToRoomAsync(
            new PetRespectNotificationEventMessageComposer
            {
                Respect = pet.Respect,
                PetOwnerId = pet.OwnerId,
                Pet = pet.GetPetSnapshot(),
            },
            ct
        );

        await AddExperienceAsync(pet, Config.RespectExperience, ct);

        if (pet.IsMonsterplant)
        {
            await PersistAsync(pet, ct);
            await SendInfoToOwnerAsync(pet, ct);
        }

        return true;
    }

    public async Task<bool> GivePetSupplementAsync(
        ActionContext ctx,
        int petId,
        PetSupplementType supplement,
        CancellationToken ct
    )
    {
        if (!TryGetPet(petId, out var pet) || !pet.IsMonsterplant || pet.OwnerId != ctx.PlayerId)
            return false;

        switch (supplement)
        {
            case PetSupplementType.Water:
                pet.SetWateredAt(DateTime.UtcNow);
                break;
            case PetSupplementType.Light:
                pet.SetEnergy(
                    Math.Min(Config.MaxEnergy, pet.Energy + Config.MonsterplantSupplementEnergy)
                );
                break;
            default:
                return false;
        }

        RefreshFlags(pet);

        await _roomGrain.SendComposerToRoomAsync(
            new PetSupplementedNotificationEventMessageComposer
            {
                PetId = petId,
                PlayerId = ctx.PlayerId,
                Supplement = supplement,
            },
            ct
        );
        await BroadcastStatusAsync(pet, ct);
        await PersistAsync(pet, ct);
        await SendInfoToOwnerAsync(pet, ct);

        return true;
    }

    public async Task<bool> HarvestPetAsync(ActionContext ctx, int petId, CancellationToken ct)
    {
        if (!TryGetPet(petId, out var pet) || !pet.IsMonsterplant || pet.OwnerId != ctx.PlayerId)
            return false;

        RefreshFlags(pet);

        if (!pet.CanHarvest)
            return false;

        var definition = _roomGrain._definitionProvider.TryGetDefinitionByName(
            Config.MonsterplantSeedDefinitionName
        );

        if (definition is null)
        {
            _roomGrain._logger.LogError(
                "Seed definition {Name} is missing; monsterplant {PetId} cannot be harvested",
                Config.MonsterplantSeedDefinitionName,
                petId
            );

            return false;
        }

        var seed = await _roomGrain
            ._grainFactory.GetInventoryGrain(ctx.PlayerId)
            .GrantFurnitureAsync(definition.Id, null, ct);

        if (seed is null)
            return false;

        pet.SetHarvestedAt(DateTime.UtcNow);

        RefreshFlags(pet);

        await BroadcastStatusAsync(pet, ct);
        await PersistAsync(pet, ct);

        return true;
    }

    public async Task<bool> CompostPetAsync(ActionContext ctx, int petId, CancellationToken ct)
    {
        if (!TryGetPet(petId, out var pet) || !pet.IsMonsterplant || pet.OwnerId != ctx.PlayerId)
            return false;

        RefreshFlags(pet);

        if (!pet.CanRevive)
            return false;

        await RemovePetAvatarAsync(ctx, pet, ct);

        return await _roomGrain
            ._grainFactory.GetInventoryGrain(pet.OwnerId)
            .DeletePetAsync(petId, ct);
    }

    internal async Task ReviveAsync(IRoomPet pet, CancellationToken ct)
    {
        pet.SetWateredAt(DateTime.UtcNow);

        RefreshFlags(pet);

        await BroadcastStatusAsync(pet, ct);
        await PersistAsync(pet, ct);
        await SendInfoToOwnerAsync(pet, ct);
    }

    public async Task<bool> PassHandItemToPetAsync(
        ActionContext ctx,
        int petId,
        CancellationToken ct
    )
    {
        if (
            !TryGetPet(petId, out var pet)
            || !_roomGrain.AvatarModule.TryGetPlayer(ctx.PlayerId, out var player)
        )
            return false;

        if (
            player.HandItemId <= 0
            || pet.IsMonsterplant
            || !RoomAvatarModule.AreAdjacent(pet, player)
        )
            return false;

        await _roomGrain.AvatarModule.SetHandItemAsync(player, 0, ct);

        pet.SetNutrition(Math.Min(Config.MaxNutrition, pet.Nutrition + Config.HandItemNutrition));
        pet.SetEnergy(Math.Min(Config.MaxEnergy, pet.Energy + Config.HandItemEnergy));

        ClearActionStatuses(pet);

        pet.AddStatus(AvatarStatusType.Eat, string.Empty);
        pet.ActionExpiresAtMs = _roomGrain.NowMs() + Config.HandItemEatDurationMs;

        await PersistAsync(pet, ct);

        return true;
    }

    /// <summary>The pet arrived at the item it was walking to.</summary>
    internal async Task OnPetReachedItemAsync(IRoomPet pet, CancellationToken ct)
    {
        var itemId = pet.TargetItemId;

        pet.TargetItemId = -1;

        if (!_roomGrain._state.ItemsById.TryGetValue(itemId, out var item))
            return;

        switch (item.Logic)
        {
            case IPetSupplyLogic supply:
                await supply.OnPetReachedAsync(pet, ct);
                break;
            case FurniturePetBreedingNestLogic nest:
                await OnPetReachedNestAsync(pet, nest, ct);
                break;
        }
    }
}
