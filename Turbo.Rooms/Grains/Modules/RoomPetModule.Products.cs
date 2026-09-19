using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Turbo.Primitives.Action;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Pets;
using Turbo.Primitives.Pets.Snapshots;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Object.Furniture;

namespace Turbo.Rooms.Grains.Modules;

public sealed partial class RoomPetModule
{
    /// <summary>
    /// Plants a monsterplant seed: a plant of a breed rolled by rarity is created in the
    /// owner's inventory and placed where the seed stood, and the seed is consumed.
    /// </summary>
    internal async Task<bool> PlantSeedAsync(
        ActionContext ctx,
        IRoomItem seed,
        int minRarityLevel,
        CancellationToken ct
    )
    {
        if (Pets.Count() >= Config.MaxPetsPerRoom)
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

        var plant = await _roomGrain
            ._grainFactory.GetInventoryGrain(ctx.PlayerId)
            .CreatePetAsync(
                Config.MonsterplantDefaultName,
                PetTypes.MONSTERPLANT,
                palette.PaletteId,
                palette.BreedId,
                PetFigure.DEFAULT_COLOR,
                palette.RarityLevel,
                ct
            );

        if (plant is null)
            return false;

        await _roomGrain.ActionModule.DeleteItemByIdAsync(ctx, seed.ObjectId, ct);

        return await PlacePetAsync(ctx, plant.Id, x, y, ct);
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

        pet.SetPetFigure(pet.PetFigure with { CustomParts = builder.ToImmutable() });

        await BroadcastFigureAsync(pet, ct);
        await PersistAsync(pet, ct);
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
        await PersistAsync(pet, ct);

        return true;
    }

    /// <summary>Fertilizer: a growing monsterplant advances a level and its growth clock restarts.</summary>
    internal async Task FertilizeAsync(IRoomPet pet, CancellationToken ct)
    {
        await LevelUpAsync(pet, ct);
        await PersistAsync(pet, ct);
        await SendInfoToOwnerAsync(pet, ct);
    }

    /// <summary>Returns every pet to its owner's inventory, as a room deletion requires.</summary>
    internal async Task ReturnAllToOwnersAsync(CancellationToken ct)
    {
        foreach (var pet in Pets.ToList())
        {
            await RemovePetAvatarAsync(ActionContext.CreateForSystem(_roomGrain.RoomId), pet, ct);

            if (
                !await _roomGrain
                    ._grainFactory.GetInventoryGrain(pet.OwnerId)
                    .ReturnPetAsync(pet.GetPetSnapshot(), ct)
            )
                _roomGrain._logger.LogError(
                    "Pet {PetId} could not be returned to player {OwnerId} while room {RoomId} is deleted",
                    pet.PetId,
                    pet.OwnerId,
                    _roomGrain.RoomId
                );
        }
    }
}
