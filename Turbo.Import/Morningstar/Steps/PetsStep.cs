using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Entities.Pets;
using Turbo.Import.Morningstar.Formats;
using Turbo.Import.Source;
using Turbo.Primitives.Pets;

namespace Turbo.Import.Morningstar.Steps;

/// <summary>
/// <c>users_pets</c> to <c>pets</c>, ids kept: in their room where it was copied, else in the
/// owner's inventory. Morningstar's type and race are the client's pet type and palette, as
/// Turbo's are. The custom parts are what Morningstar drew the pet with: a horse's mane and tail
/// (and the saddle's layer), a monsterplant's body, mouth, nose and eyes. Morningstar counts
/// hunger up, Turbo nutrition down; thirst and happiness Turbo does not have.
/// </summary>
internal sealed class PetsStep : IImportStep
{
    private const string SADDLE_LOGIC = "pet_saddle";

    // Morningstar draws a saddled horse with layer 4, part 9.
    private static readonly int[] SADDLE_PARTS = [4, 9, 0];

    public string Name => "pets";

    public async Task RunAsync(ImportContext ctx, CancellationToken ct)
    {
        var breeds = await ctx
            .Db.PetBreeds.AsNoTracking()
            .ToDictionaryAsync(x => (x.TypeId, x.PaletteId), ct)
            .ConfigureAwait(false);

        var anySaddle = ctx.Definitions.Values.FirstOrDefault(x => x.Logic == SADDLE_LOGIC)?.Id;

        var batch = ctx.Batch<PetEntity>("pets");

        await foreach (
            var row in ctx.Source.ReadAsync("users_pets", "id", ct).ConfigureAwait(false)
        )
        {
            var id = row.Int("id");
            var owner = row.Int("user_id");

            if (!ctx.Players.Contains(owner))
            {
                ctx.Report.Note("pet left out: its owner was not copied", $"pet {id}");

                continue;
            }

            var type = row.Int("type");
            var palette = row.Int("race");
            var room = row.Int("room_id");
            var inRoom = room > 0 && ctx.RoomOwners.ContainsKey(room);
            var created = row.UnixTime("created") ?? ctx.Now;
            var breed = breeds.GetValueOrDefault((type, palette));
            var saddled = type == PetTypes.HORSE && row.Bool("saddle");
            var color = row.Text("color").Trim();

            var pet = new PetEntity
            {
                Id = id,
                CreatedAt = created,
                PlayerEntityId = owner,
                RoomEntityId = inRoom ? room : null,
                Name = Cut(row.Text("name", "Pet").Trim(), PetEntity.NAME_MAX_LENGTH),
                TypeId = type,
                PaletteId = palette,
                BreedId = breed?.BreedId ?? palette,
                Color = PetFigure.IsValidColor(color)
                    ? color.ToUpperInvariant()
                    : PetFigure.DEFAULT_COLOR,
                CustomParts = CustomParts(row, type, saddled),
                Experience = Math.Max(0, row.Int("experience")),
                Energy = Math.Clamp(row.Int("energy", 100), 0, 100),
                Nutrition = Math.Clamp(100 - row.Int("hunger"), 0, 100),
                Respect = Math.Max(0, row.Int("respect")),
                RarityLevel = breed?.RarityLevel ?? 0,
                HasSaddle = saddled,
                SaddleDefinitionId = saddled
                    ? ctx.Definitions.GetValueOrDefault(row.Int("saddle_item_id"))?.Id ?? anySaddle
                    : null,
                AnyoneCanRide = row.Bool("ride"),
                HasBreedingPermission = row.Bool("mp_allow_breed"),
                X = inRoom ? row.Int("x") : 0,
                Y = inRoom ? row.Int("y") : 0,
                Z = inRoom ? row.Double("z") : 0,
                Rotation = Directions.Rotation(inRoom ? row.Int("rot") : 0),
                WateredAt = ctx.Now,
            };

            pet.Level = PetTypes.IsMonsterplant(type)
                ? PlantLevel(ctx, created)
                : Level(ctx.Options.Pets, pet.Experience);

            if (PetTypes.IsMonsterplant(type))
                pet.WateredAt = WateredAt(ctx, row, pet.Level);

            if (room > 0 && !inRoom)
                ctx.Report.Note(
                    "pet put in its owner's inventory: its room was not copied",
                    $"pet {id} room {room}"
                );

            await batch.AddAsync(pet, ct).ConfigureAwait(false);
        }

        await batch.FlushAsync(ct).ConfigureAwait(false);
    }

    private static string? CustomParts(SourceRow row, int type, bool saddled)
    {
        List<int> parts = [];

        if (type == PetTypes.HORSE)
        {
            var style = row.Int("hair_style", -1);
            var color = row.Int("hair_color");

            parts.AddRange([2, style, color, 3, style, color]);

            if (saddled)
                parts.AddRange(SADDLE_PARTS);
        }
        else if (PetTypes.IsMonsterplant(type))
            parts.AddRange([
                1,
                row.Int("mp_type", 1),
                row.Int("mp_color"),
                2,
                row.Int("mp_mouth"),
                row.Int("mp_mouth_color"),
                3,
                row.Int("mp_nose"),
                row.Int("mp_nose_color"),
                4,
                row.Int("mp_eyes"),
                row.Int("mp_eyes_color"),
            ]);

        if (parts.Count == 0)
            return null;

        var stored = PetFigure.SerializeCustomParts([.. parts]);

        return stored.Length > PetEntity.CUSTOM_PARTS_MAX_LENGTH ? null : stored;
    }

    /// <summary>Level 1, plus one for each threshold the experience has reached.</summary>
    internal static int Level(PetRules rules, int experience)
    {
        var level = 1;

        foreach (var threshold in rules.LevelExperience)
        {
            if (experience < threshold)
                break;

            level++;
        }

        return Math.Clamp(level, 1, Math.Max(1, rules.MaxLevel));
    }

    /// <summary>A monsterplant grows a level a growth period from when it was planted.</summary>
    private static int PlantLevel(ImportContext ctx, DateTime planted)
    {
        var rules = ctx.Options.Pets;
        var grown = (ctx.Now - planted).TotalSeconds / Math.Max(1, rules.MonsterplantGrowthSeconds);

        return Math.Clamp(1 + (int)Math.Floor(Math.Max(0, grown)), 1, rules.MonsterplantMaxLevel);
    }

    /// <summary>
    /// When the plant was last watered, so its wellbeing runs out when Morningstar's
    /// <c>mp_death_timestamp</c> said; a dead plant was watered a whole wellbeing ago.
    /// </summary>
    private static DateTime WateredAt(ImportContext ctx, SourceRow row, int level)
    {
        var rules = ctx.Options.Pets;
        var wellBeing =
            level >= rules.MonsterplantMaxLevel
                ? rules.MonsterplantWellBeingSeconds
                : rules.MonsterplantBabyWellBeingSeconds;

        if (row.Bool("mp_is_dead"))
            return ctx.Now.AddSeconds(-wellBeing);

        var dies = row.UnixTime("mp_death_timestamp");

        if (dies is null)
            return ctx.Now;

        var watered = dies.Value.AddSeconds(-wellBeing);

        return watered > ctx.Now ? ctx.Now : watered;
    }

    private static string Cut(string value, int length) =>
        value.Length > length ? value[..length] : value;
}
