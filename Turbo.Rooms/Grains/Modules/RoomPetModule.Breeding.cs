using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Outgoing.Inventory.Pets;
using Turbo.Primitives.Messages.Outgoing.Room.Pets;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Pets;
using Turbo.Primitives.Pets.Enums;
using Turbo.Primitives.Pets.Snapshots;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Pets;

namespace Turbo.Rooms.Grains.Modules;

public sealed partial class RoomPetModule
{
    /// <summary>The "breed" command: the pet walks to a nest that is empty or holds one of its kind.</summary>
    internal async Task<bool> GoToBreedingNestAsync(
        ActionContext ctx,
        IRoomPet pet,
        CancellationToken ct
    )
    {
        RefreshFlags(pet);

        if (!pet.CanBreed)
            return false;

        var nests = _roomGrain
            ._state.ItemsById.Values.Where(x =>
                x.Logic is FurniturePetBreedingNestLogic nest && nest.CanAccept(pet)
            )
            .OrderByDescending(x => ((FurniturePetBreedingNestLogic)x.Logic).OccupantPetIds.Count)
            .ToList();

        if (nests.Count == 0)
        {
            await _roomGrain._grainFactory.SendComposerToPlayerAsync(
                ctx.PlayerId,
                new GoToBreedingNestFailureEventMessageComposer
                {
                    Reason = BreedingNestFailureType.NoFreeNest,
                },
                ct
            );

            return false;
        }

        await LeaveNestAsync(pet, ct);

        var nestItem = nests[0];

        pet.TargetItemId = nestItem.ObjectId;

        if (pet.X == nestItem.X && pet.Y == nestItem.Y)
        {
            await OnPetReachedItemAsync(pet, ct);

            return true;
        }

        if (await _roomGrain.AvatarModule.WalkAvatarToAsync(pet, nestItem.X, nestItem.Y, ct))
            return true;

        pet.TargetItemId = -1;

        return false;
    }

    internal async Task OnPetReachedNestAsync(
        IRoomPet pet,
        FurniturePetBreedingNestLogic nest,
        CancellationToken ct
    )
    {
        if (!nest.AddOccupant(pet))
            return;

        pet.Lay(true);
        pet.IsFreeRoaming = false;

        if (nest.OccupantPetIds.Count < FurniturePetBreedingNestLogic.CAPACITY)
            return;

        await StartNestSessionAsync(nest, ct);
    }

    private async Task StartNestSessionAsync(
        FurniturePetBreedingNestLogic nest,
        CancellationToken ct
    )
    {
        var nestId = nest.Context.ObjectId;

        if (_roomGrain._state.PendingNestBreedings.ContainsKey(nestId))
            return;

        if (
            !TryGetPet(nest.OccupantPetIds[0], out var pet1)
            || !TryGetPet(nest.OccupantPetIds[1], out var pet2)
        )
            return;

        var session = new NestBreedingSession
        {
            NestId = nestId,
            Pet1Id = pet1.PetId,
            Pet2Id = pet2.PetId,
            OwnerIds = [pet1.OwnerId, pet2.OwnerId],
        };

        _roomGrain._state.PendingNestBreedings[nestId] = session;

        var request = new ConfirmBreedingRequestEventMessageComposer
        {
            NestId = nestId,
            Pet1 = ToBreedingPet(pet1),
            Pet2 = ToBreedingPet(pet2),
            RarityCategories = RarityCategoriesFor(pet1.TypeId),
            ResultPetTypeId = pet1.TypeId,
        };

        foreach (var ownerId in session.OwnerIds)
            await _roomGrain._grainFactory.SendComposerToPlayerAsync(ownerId, request, ct);
    }

    public async Task<bool> ConfirmNestBreedingAsync(
        ActionContext ctx,
        RoomObjectId nestId,
        string name,
        int petId,
        int otherPetId,
        CancellationToken ct
    )
    {
        if (
            !_roomGrain._state.PendingNestBreedings.TryGetValue(nestId, out var session)
            || !session.OwnerIds.Contains(ctx.PlayerId)
            || !session.Involves(petId)
            || !session.Involves(otherPetId)
        )
        {
            await SendResultAsync(ctx.PlayerId, nestId, ConfirmBreedingResultType.PetsMissing, ct);

            return false;
        }

        if (!TryGetPet(session.Pet1Id, out var pet1) || !TryGetPet(session.Pet2Id, out var pet2))
        {
            await EndNestSessionAsync(session, ConfirmBreedingResultType.PetsMissing, ct);

            return false;
        }

        var status = PetNames.Validate(name, Config.NameMinLength, Config.NameMaxLength);

        if (status != PetNameValidationType.Ok)
        {
            await SendResultAsync(ctx.PlayerId, nestId, ConfirmBreedingResultType.InvalidName, ct);

            return true;
        }

        session.Name = name.Trim();
        session.ConfirmedOwnerIds.Add(ctx.PlayerId);

        if (!session.IsConfirmedByAll)
            return true;

        var offspringPalette = PickOffspringPalette(pet1, pet2);

        var offspring = await _roomGrain
            ._grainFactory.GetInventoryGrain(pet1.OwnerId)
            .CreatePetAsync(
                session.Name,
                pet1.TypeId,
                offspringPalette.PaletteId,
                offspringPalette.BreedId,
                Chance(50) ? pet1.PetFigure.Color : pet2.PetFigure.Color,
                offspringPalette.RarityLevel,
                ct
            );
        var rarityLevel = offspringPalette.RarityLevel;

        if (offspring is null)
        {
            _roomGrain._logger.LogWarning(
                "Nest breeding in room {RoomId} produced no pet for player {OwnerId}; the inventory refused it",
                _roomGrain.RoomId,
                pet1.OwnerId
            );

            await EndNestSessionAsync(session, ConfirmBreedingResultType.PetsMissing, ct);

            return false;
        }

        foreach (var ownerId in session.OwnerIds)
            await _roomGrain._grainFactory.SendComposerToPlayerAsync(
                ownerId,
                new NestBreedingSuccessEventMessageComposer
                {
                    PetId = offspring.Id,
                    RarityCategory = rarityLevel,
                },
                ct
            );

        await EndNestSessionAsync(session, ConfirmBreedingResultType.Ok, ct);

        return true;
    }

    public async Task<bool> CancelNestBreedingAsync(
        ActionContext ctx,
        RoomObjectId nestId,
        CancellationToken ct
    )
    {
        if (
            !_roomGrain._state.PendingNestBreedings.TryGetValue(nestId, out var session)
            || !session.OwnerIds.Contains(ctx.PlayerId)
        )
            return false;

        await EndNestSessionAsync(session, ConfirmBreedingResultType.Ok, ct);

        return true;
    }

    /// <summary>Closes a session: every owner hears the result and both pets are freed from the nest.</summary>
    private async Task EndNestSessionAsync(
        NestBreedingSession session,
        ConfirmBreedingResultType result,
        CancellationToken ct
    )
    {
        _roomGrain._state.PendingNestBreedings.Remove(session.NestId);

        foreach (var ownerId in session.OwnerIds)
            await SendResultAsync(ownerId, session.NestId, result, ct);

        foreach (var petId in new[] { session.Pet1Id, session.Pet2Id })
        {
            if (TryGetPet(petId, out var pet))
                ReleaseFromNest(pet);
        }
    }

    private Task SendResultAsync(
        PlayerId playerId,
        RoomObjectId nestId,
        ConfirmBreedingResultType result,
        CancellationToken ct
    ) =>
        _roomGrain._grainFactory.SendComposerToPlayerAsync(
            playerId,
            new ConfirmBreedingResultEventMessageComposer { NestId = nestId, Result = result },
            ct
        );

    /// <summary>Takes the pet out of any nest it occupies, ending a breeding that depended on it.</summary>
    internal async Task LeaveNestAsync(IRoomPet pet, CancellationToken ct)
    {
        var session = _roomGrain._state.PendingNestBreedings.Values.FirstOrDefault(x =>
            x.Involves(pet.PetId)
        );

        if (session is not null)
            await EndNestSessionAsync(session, ConfirmBreedingResultType.PetsMissing, ct);

        ReleaseFromNest(pet);
    }

    private void ReleaseFromNest(IRoomPet pet)
    {
        foreach (var item in _roomGrain._state.ItemsById.Values)
        {
            if (item.Logic is FurniturePetBreedingNestLogic nest)
                nest.RemoveOccupant(pet.PetId);
        }

        pet.Lay(false);
        pet.IsFreeRoaming = true;
    }

    /// <summary>The nest was picked up from under its occupants.</summary>
    internal async Task OnNestRemovedAsync(RoomObjectId nestId, CancellationToken ct)
    {
        if (_roomGrain._state.PendingNestBreedings.TryGetValue(nestId, out var session))
            await EndNestSessionAsync(session, ConfirmBreedingResultType.NoNest, ct);

        if (
            _roomGrain._state.ItemsById.TryGetValue(nestId, out var item)
            && item.Logic is FurniturePetBreedingNestLogic nest
        )
        {
            foreach (var petId in nest.OccupantPetIds.ToList())
            {
                if (TryGetPet(petId, out var pet))
                    ReleaseFromNest(pet);
            }
        }
    }

    private PetBreedingPetSnapshot ToBreedingPet(IRoomPet pet) =>
        new()
        {
            PetId = pet.PetId,
            Name = pet.Name,
            Level = pet.Level,
            Figure = PetFigure.ToFigureString(pet.PetFigure),
            OwnerName = pet.OwnerName,
        };

    private ImmutableArray<PetBreedingRarityCategorySnapshot> RarityCategoriesFor(int typeId) =>
        [
            .. _roomGrain
                ._petBreedProvider.GetPalettes(typeId)
                .GroupBy(x => x.RarityLevel)
                .OrderBy(x => x.Key)
                .Select(group => new PetBreedingRarityCategorySnapshot
                {
                    RarityLevel = group.Key,
                    Chance = ChanceForRarity(group.Key),
                    PaletteIds = [.. group.Select(x => x.PaletteId)],
                }),
        ];

    private int ChanceForRarity(int rarityLevel) =>
        rarityLevel >= 0 && rarityLevel < Config.BreedingRarityChances.Length
            ? Config.BreedingRarityChances[rarityLevel]
            : 0;

    /// <summary>Rolls a rarity by its chance, then a palette within it; a parent's look when no table exists.</summary>
    private PetBreedSnapshot PickOffspringPalette(IRoomPet pet1, IRoomPet pet2)
    {
        var parent = Chance(50) ? pet1 : pet2;

        return PickPaletteByRarity(
            pet1.TypeId,
            new PetBreedSnapshot
            {
                TypeId = parent.TypeId,
                BreedId = parent.PetFigure.BreedId,
                PaletteId = parent.PetFigure.PaletteId,
                RarityLevel = parent.RarityLevel,
                Sellable = false,
                Rare = false,
                ColorTag = -1,
            }
        );
    }

    /// <summary>
    /// Rolls a rarity by the configured chances, then a palette within it. With no palette
    /// table for the type the fallback is returned as is.
    /// </summary>
    internal PetBreedSnapshot PickPaletteByRarity(
        int typeId,
        PetBreedSnapshot fallback,
        int minRarityLevel = 0
    )
    {
        var all = RarityCategoriesFor(typeId);
        var eligible = all.Where(x => x.RarityLevel >= minRarityLevel).ToImmutableArray();

        // A rarity floor above every category falls back to the whole table.
        var categories = eligible.Length > 0 ? eligible : all;

        if (categories.Length == 0)
            return fallback;

        var total = categories.Sum(x => x.Chance);
        var roll = NextRandom(0, System.Math.Max(1, total));
        var picked = categories[0];

        foreach (var category in categories)
        {
            if (roll < category.Chance)
            {
                picked = category;

                break;
            }

            roll -= category.Chance;
        }

        var paletteId = picked.PaletteIds[NextRandom(0, picked.PaletteIds.Length)];

        return _roomGrain._petBreedProvider.TryGetPalette(typeId, paletteId) ?? fallback;
    }

    /// <summary>Monsterplant breeding: a request the other plant's owner accepts or declines.</summary>
    public async Task<bool> BreedPetsAsync(
        ActionContext ctx,
        PetBreedingAction action,
        int petId,
        int otherPetId,
        CancellationToken ct
    )
    {
        if (
            petId == otherPetId
            || !TryGetPet(petId, out var pet)
            || !TryGetPet(otherPetId, out var other)
            || !pet.IsMonsterplant
            || !other.IsMonsterplant
        )
            return false;

        switch (action)
        {
            case PetBreedingAction.Request:
                return await RequestPlantBreedingAsync(ctx, pet, other, ct);
            case PetBreedingAction.Cancel:
                return await CancelPlantBreedingAsync(ctx, pet, other, ct);
            case PetBreedingAction.Accept:
                return await AcceptPlantBreedingAsync(ctx, pet, other, ct);
            default:
                return false;
        }
    }

    private async Task<bool> RequestPlantBreedingAsync(
        ActionContext ctx,
        IRoomPet pet,
        IRoomPet other,
        CancellationToken ct
    )
    {
        if (pet.OwnerId != ctx.PlayerId)
            return false;

        RefreshFlags(pet);
        RefreshFlags(other);

        if (!pet.CanBreed || !other.CanBreed)
            return false;

        if (other.OwnerId != ctx.PlayerId && !other.HasBreedingPermission)
            return false;

        if (
            _roomGrain._state.PendingPlantBreedings.Values.Any(x =>
                x.Involves(pet.PetId) || x.Involves(other.PetId)
            )
        )
            return false;

        _roomGrain._state.PendingPlantBreedings[pet.PetId] = new PlantBreedingRequest
        {
            PetId = pet.PetId,
            OtherPetId = other.PetId,
            RequesterId = ctx.PlayerId,
            OtherOwnerId = other.OwnerId,
        };

        await _roomGrain._grainFactory.SendComposerToPlayerAsync(
            other.OwnerId,
            new PetBreedingEventMessageComposer
            {
                State = PetBreedingState.Requested,
                OwnPetId = other.PetId,
                OtherPetId = pet.PetId,
            },
            ct
        );
        await _roomGrain._grainFactory.SendComposerToPlayerAsync(
            ctx.PlayerId,
            new PetBreedingEventMessageComposer
            {
                State = PetBreedingState.RequestedByMe,
                OwnPetId = pet.PetId,
                OtherPetId = other.PetId,
            },
            ct
        );

        return true;
    }

    private async Task<bool> CancelPlantBreedingAsync(
        ActionContext ctx,
        IRoomPet pet,
        IRoomPet other,
        CancellationToken ct
    )
    {
        var request = FindPlantRequest(pet.PetId, other.PetId);

        if (
            request is null
            || (request.RequesterId != ctx.PlayerId && request.OtherOwnerId != ctx.PlayerId)
        )
            return false;

        await EndPlantBreedingAsync(request, PetBreedingState.Cancelled, ct);

        return true;
    }

    private async Task<bool> AcceptPlantBreedingAsync(
        ActionContext ctx,
        IRoomPet pet,
        IRoomPet other,
        CancellationToken ct
    )
    {
        var request = FindPlantRequest(pet.PetId, other.PetId);

        if (request is null || request.OtherOwnerId != ctx.PlayerId)
            return false;

        var definition = _roomGrain._definitionProvider.TryGetDefinitionByName(
            Config.MonsterplantSeedDefinitionName
        );

        if (definition is null)
        {
            _roomGrain._logger.LogError(
                "Seed definition {Name} is missing; monsterplants {PetId} and {OtherPetId} cannot breed",
                Config.MonsterplantSeedDefinitionName,
                pet.PetId,
                other.PetId
            );

            await EndPlantBreedingAsync(request, PetBreedingState.Cancelled, ct);

            return false;
        }

        await EndPlantBreedingAsync(request, PetBreedingState.Accepted, ct);

        if (
            !TryGetPet(request.PetId, out var requesterPlant)
            || !TryGetPet(request.OtherPetId, out var otherPlant)
        )
            return false;

        var requesterSeed = await _roomGrain
            ._grainFactory.GetInventoryGrain(request.RequesterId)
            .GrantFurnitureAsync(definition.Id, null, ct);
        var otherSeed = await _roomGrain
            ._grainFactory.GetInventoryGrain(request.OtherOwnerId)
            .GrantFurnitureAsync(definition.Id, null, ct);

        if (requesterSeed is null || otherSeed is null)
            return false;

        var requesterResult = new PetBreedingResultSnapshot
        {
            StuffId = requesterSeed.ItemId,
            ClassId = definition.SpriteId,
            ProductCode = definition.Name,
            OwnerId = request.RequesterId,
            OwnerName = requesterPlant.OwnerName,
            RarityLevel = requesterPlant.RarityLevel,
            HasMutation = false,
        };
        var otherResult = new PetBreedingResultSnapshot
        {
            StuffId = otherSeed.ItemId,
            ClassId = definition.SpriteId,
            ProductCode = definition.Name,
            OwnerId = request.OtherOwnerId,
            OwnerName = otherPlant.OwnerName,
            RarityLevel = otherPlant.RarityLevel,
            HasMutation = false,
        };

        await _roomGrain._grainFactory.SendComposerToPlayerAsync(
            request.RequesterId,
            new PetBreedingResultEventMessageComposer
            {
                Result = requesterResult,
                OtherResult = otherResult,
            },
            ct
        );

        if (request.OtherOwnerId != request.RequesterId)
            await _roomGrain._grainFactory.SendComposerToPlayerAsync(
                request.OtherOwnerId,
                new PetBreedingResultEventMessageComposer
                {
                    Result = otherResult,
                    OtherResult = requesterResult,
                },
                ct
            );

        return true;
    }

    private PlantBreedingRequest? FindPlantRequest(int petId, int otherPetId) =>
        _roomGrain._state.PendingPlantBreedings.Values.FirstOrDefault(x =>
            x.Involves(petId) && x.Involves(otherPetId)
        );

    private async Task EndPlantBreedingAsync(
        PlantBreedingRequest request,
        PetBreedingState state,
        CancellationToken ct
    )
    {
        _roomGrain._state.PendingPlantBreedings.Remove(request.PetId);

        await _roomGrain._grainFactory.SendComposerToPlayerAsync(
            request.RequesterId,
            new PetBreedingEventMessageComposer
            {
                State = state,
                OwnPetId = request.PetId,
                OtherPetId = request.OtherPetId,
            },
            ct
        );

        if (request.OtherOwnerId != request.RequesterId)
            await _roomGrain._grainFactory.SendComposerToPlayerAsync(
                request.OtherOwnerId,
                new PetBreedingEventMessageComposer
                {
                    State = state,
                    OwnPetId = request.OtherPetId,
                    OtherPetId = request.PetId,
                },
                ct
            );
    }

    internal async Task CancelPlantBreedingsInvolvingAsync(int petId, CancellationToken ct)
    {
        var requests = _roomGrain
            ._state.PendingPlantBreedings.Values.Where(x => x.Involves(petId))
            .ToList();

        foreach (var request in requests)
            await EndPlantBreedingAsync(request, PetBreedingState.Cancelled, ct);
    }
}
