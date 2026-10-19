using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Interactions;
using Turbo.Primitives.Inventory.Grains;
using Turbo.Primitives.Inventory.Snapshots;
using Turbo.Primitives.Movements;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Pets;
using Turbo.Primitives.Pets.Snapshots;
using Turbo.Primitives.Quests;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Object.Furniture;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Pets;

namespace Turbo.Rooms.Grains.Modules;

public sealed partial class RoomPetModule
{
    /// <summary>
    /// Plants a monsterplant seed: a plant of a breed rolled by rarity, in one of the twelve
    /// shapes, is created in the owner's inventory and placed where the seed stood, and the seed
    /// is consumed.
    /// </summary>
    internal async Task<bool> PlantSeedAsync(
        ActionContext ctx,
        IRoomItem seed,
        int minRarityLevel,
        CancellationToken ct
    )
    {
        if (Pets.Count >= Config.MaxPetsPerRoom)
        {
            _roomGrain._logger.LogDebug(
                "Seed {ItemId} in room {RoomId} not planted: the room holds its maximum of pets",
                seed.ObjectId,
                _roomGrain.RoomId
            );

            return false;
        }

        var palette = PickPaletteByRarity(
            PetTypes.MONSTERPLANT,
            new PetBreedSnapshot
            {
                TypeId = PetTypes.MONSTERPLANT,
                BreedId = 0,
                PaletteId = 0,
                RarityLevel = 0,
                Sellable = false,
                Rare = false,
                ColorTag = -1,
            },
            minRarityLevel
        );
        var (x, y) = (seed.X, seed.Y);

        // Used up before the plant is created: the plant is written at once, so a seed deleted
        // after it outlived it by a write and a crash in between left both.
        // One operation: the seed out of the room, then the plant into the inventory.
        var cause = MovementCause.New(
            MovementCauses.USE_MONSTERPLANT_SEED,
            ctx.PlayerId,
            seed.ObjectId.Value
        );

        await ActionModule.DeleteItemByIdAsync(ctx, seed.ObjectId, cause, ct);

        var plant = await _roomGrain
            ._grainFactory.GetInventoryGrain(ctx.PlayerId)
            .CreatePetAsync(
                Config.MonsterplantDefaultName,
                PetTypes.MONSTERPLANT,
                palette.PaletteId,
                palette.BreedId,
                PetFigure.DEFAULT_COLOR,
                palette.RarityLevel,
                MonsterplantFigure.CustomParts(
                    NextRandom(MonsterplantFigure.MIN_SHAPE, MonsterplantFigure.MAX_SHAPE + 1),
                    palette.PaletteId
                ),
                cause.StartingAt(cause.FirstSeq + 1),
                ct
            );

        if (plant is null)
        {
            await ActionModule.GiveBackUsedItemAsync(seed, ct);

            return false;
        }

        if (!await PlacePetAsync(ctx, plant.Id, x, y, ct))
            return false;

        _roomGrain._grainFactory.CountRewardTrackAction(
            ctx.PlayerId,
            RewardTrackActionTypes.PLANT_MONSTER_PLANT,
            _roomGrain._logger
        );

        return true;
    }

    /// <summary>
    /// A pet product used from the inventory (Flash's use button, <c>FurniModel.showUseProductSelection</c>):
    /// the room gives the item its logic without placing it and asks the product what it asks
    /// one standing in the room (<see cref="FurniturePetProductLogic.TryGetPetToTake"/>). The
    /// item is used up first, so one traded or placed meanwhile changes no pet for nothing, and
    /// is given back when the product then has no effect. A rented item is never used up: it is
    /// not the player's for good, the rule the room holds for borrowed and temporary furni.
    /// </summary>
    internal async Task<bool> UsePetProductFromInventoryAsync(
        ActionContext ctx,
        RoomObjectId itemId,
        int petId,
        CancellationToken ct
    )
    {
        var inventory = _roomGrain._grainFactory.GetInventoryGrain(ctx.PlayerId);
        var snapshot = await inventory.GetItemSnapshotAsync(itemId, ct);

        if (snapshot is null)
            return RejectInventoryProduct(ctx, itemId, petId, "not in their inventory");

        if (snapshot.IsRented)
            return RejectInventoryProduct(ctx, itemId, petId, "rented");

        // Never placed and never attached: the logic only reads the item's definition and
        // extra data and the room's pets, so nothing hears of it and the room does not change.
        if (
            snapshot.Definition.ProductType != ProductType.Floor
            || _roomGrain._itemsLoader.CreateFromFurnitureItemSnapshot(snapshot)
                is not IRoomFloorItem item
            || !ObjectModule.EnsureLogic(item)
            || item.Logic is not FurniturePetProductLogic product
        )
            return RejectInventoryProduct(ctx, itemId, petId, "not a pet product");

        var use = new UseWithPetInteraction { PetId = petId };

        if (!product.TryGetPetToTake(ctx, use, out var pet))
            return false;

        var cause = MovementCause.New(MovementCauses.USE_PET_PRODUCT, ctx.PlayerId, itemId.Value);
        var consumed = await inventory.ConsumeFurnitureAsync(itemId, cause, ct);

        if (consumed is null)
            return RejectInventoryProduct(ctx, itemId, petId, "the inventory no longer holds it");

        bool applied;

        try
        {
            applied = await product.TryApplyAsync(ctx, use, pet, ct);
        }
        catch (Exception ex)
        {
            _roomGrain._logger.LogError(
                ex,
                "Failed to use item {ItemId} of player {PlayerId} on pet {PetId} in room {RoomId}; giving it back",
                itemId,
                ctx.PlayerId,
                petId,
                _roomGrain.RoomId
            );

            applied = false;
        }

        if (!applied)
        {
            await GiveBackConsumedAsync(inventory, consumed, cause, ct);

            return false;
        }

        // The item's row went with the inventory's answer; the pet it changed is written now
        // too, rather than on the next hand-over, as a use in the room writes both at once.
        await PersistNowAsync(ct);

        return true;
    }

    /// <summary>
    /// Puts an inventory item that was used up on a pet that then took nothing back where it
    /// was. Nowhere else to put it: a failed give-back is logged with what was lost.
    /// </summary>
    private async Task GiveBackConsumedAsync(
        IInventoryGrain inventory,
        FurnitureItemSnapshot consumed,
        MovementCause cause,
        CancellationToken ct
    )
    {
        try
        {
            await inventory.GrantFurnitureAsync(
                consumed.Definition.Id,
                consumed.ExtraData,
                cause.ForRefund(),
                ct
            );
        }
        catch (Exception ex)
        {
            _roomGrain._logger.LogError(
                ex,
                "Failed to give player {PlayerId} back definition {DefinitionId} with {ExtraData}",
                consumed.OwnerId,
                consumed.Definition.Id,
                consumed.ExtraData
            );
        }
    }

    /// <summary>Logs a refused inventory pet product with the ids that identify it, and yields false.</summary>
    private bool RejectInventoryProduct(
        ActionContext ctx,
        RoomObjectId itemId,
        int petId,
        string reason
    )
    {
        _roomGrain._logger.LogWarning(
            "Rejected using inventory item {ItemId} on pet {PetId} in room {RoomId} by player {PlayerId}: {Reason}",
            itemId,
            petId,
            _roomGrain.RoomId,
            ctx.PlayerId,
            reason
        );

        return false;
    }

    /// <summary>Replaces the custom part on one layer of the pet's figure (a new mane, a dye).</summary>
    internal async Task SetCustomPartAsync(
        IRoomPet pet,
        int layerId,
        int partId,
        int paletteId,
        CancellationToken ct
    )
    {
        var parts = pet.PetFigure.CustomParts;
        var builder = ImmutableArray.CreateBuilder<int>();

        for (
            var i = 0;
            i + PetFigure.CUSTOM_PART_FIELDS <= parts.Length;
            i += PetFigure.CUSTOM_PART_FIELDS
        )
        {
            if (parts[i] == layerId)
                continue;

            builder.Add(parts[i]);
            builder.Add(parts[i + 1]);
            builder.Add(parts[i + 2]);
        }

        builder.Add(layerId);
        builder.Add(partId);
        builder.Add(paletteId);

        await SetCustomPartsAsync(pet, builder.ToImmutable(), ct);
    }

    /// <summary>Replaces all of the pet's custom parts (a hair dye or style changing several layers).</summary>
    internal async Task SetCustomPartsAsync(
        IRoomPet pet,
        ImmutableArray<int> customParts,
        CancellationToken ct
    )
    {
        pet.SetPetFigure(pet.PetFigure with { CustomParts = customParts });

        await BroadcastFigureAsync(pet, ct);
        Persist(pet);
    }

    /// <summary>
    /// A body dye: the pet keeps its breed and moves to the palette of that breed carrying the
    /// colour tag. False when the breed has no such palette.
    /// </summary>
    internal async Task<bool> DyeAsync(IRoomPet pet, int colorTag, CancellationToken ct)
    {
        var figure = pet.PetFigure;
        var palette = _roomGrain
            ._petBreedProvider.GetPalettes(pet.TypeId)
            .FirstOrDefault(x => x.BreedId == figure.BreedId && x.ColorTag == colorTag);

        if (palette is null || palette.PaletteId == figure.PaletteId)
            return false;

        pet.SetPetFigure(figure with { PaletteId = palette.PaletteId });

        await BroadcastFigureAsync(pet, ct);
        Persist(pet);

        return true;
    }

    /// <summary>Fertilizer: a growing monsterplant advances a level and its growth clock restarts.</summary>
    internal async Task FertilizeAsync(IRoomPet pet, CancellationToken ct)
    {
        await LevelUpAsync(pet, ct);
        Persist(pet);
        await SendInfoToOwnerAsync(pet, ct);
    }
}
