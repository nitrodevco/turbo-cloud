using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Entities.Furniture;
using Turbo.Import.Morningstar.Formats;
using Turbo.Import.Source;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Import.Morningstar.Steps;

/// <summary>
/// Turbo's furniture definition for every Morningstar <c>items_base</c> row, matched the way
/// Turbo's own gamedata import matches: by type and classname (<c>item_name</c>), in any case. A
/// definition the target has is used as it is, logic and all, so a hotel that imported Habbo's
/// furnidata into Turbo first keeps Turbo's behaviour for every Habbo item. What the target lacks
/// (a hotel's custom furni) is created from Morningstar's row, its logic chosen by
/// <see cref="InteractionLogic"/>, with its vending items and crackable rewards as definition
/// sections.
/// <para>
/// Effects, badges, bots, pets and club are pseudo-items in <c>items_base</c> that never become
/// furniture; they are kept for the catalog only.
/// </para>
/// </summary>
internal sealed class FurnitureDefinitionsStep : IImportStep
{
    public const string TABLE = "furniture_definitions";

    private const int CUSTOM_PARAMS_MAX_LENGTH = 512;

    public string Name => "furniture definitions";

    public async Task RunAsync(ImportContext ctx, CancellationToken ct)
    {
        var existing = await ctx
            .Db.FurnitureDefinitions.AsNoTracking()
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.Logic,
                x.ProductType,
                x.SpriteId,
                x.FurniCategory,
            })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        // The oldest definition of a classname wins, as in Turbo's own import.
        var byName =
            new Dictionary<
                (ProductType, string),
                (int Id, string Name, string Logic, FurnitureCategory Category)
            >();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var sprites = new HashSet<(int, ProductType, FurnitureCategory)>();
        var maxSprite = new Dictionary<ProductType, int>();

        foreach (var def in existing.OrderBy(x => x.Id))
        {
            byName.TryAdd(
                (def.ProductType, def.Name.ToLowerInvariant()),
                (def.Id, def.Name, def.Logic, def.FurniCategory)
            );
            names.Add(def.Name);
            sprites.Add((def.SpriteId, def.ProductType, def.FurniCategory));
            maxSprite[def.ProductType] = Math.Max(
                maxSprite.GetValueOrDefault(def.ProductType),
                def.SpriteId
            );
        }

        var bases = await ReadBasesAsync(ctx, ct).ConfigureAwait(false);
        var clothing = await ReadClothingAsync(ctx, ct).ConfigureAwait(false);
        var crackables = await ReadCrackablesAsync(ctx, bases, ct).ConfigureAwait(false);
        var keys = ctx.Options.LogicKeys;

        foreach (var item in bases.Values)
        {
            ctx.BaseItems[item.Id] = item;

            if (item.Type is not (ProductType.Floor or ProductType.Wall))
                continue;

            if (byName.TryGetValue((item.Type, item.Name.ToLowerInvariant()), out var match))
            {
                ctx.Definitions[item.Id] = new DefinitionRef(
                    match.Id,
                    match.Name,
                    InteractionLogic.Effective(match.Logic, match.Name, keys),
                    item.Type,
                    match.Category,
                    item.Interaction
                );

                continue;
            }

            if (item.Name.Length == 0 || names.Contains(item.Name))
            {
                ctx.Report.Note(
                    "item base left out: its classname is empty or taken by the other kind of furni",
                    $"items_base {item.Id} '{item.Name}'"
                );

                continue;
            }

            var entity = Create(ctx, item, clothing, crackables);

            if (!sprites.Add((entity.SpriteId, entity.ProductType, entity.FurniCategory)))
            {
                var free = maxSprite.GetValueOrDefault(entity.ProductType) + 1;

                ctx.Report.Note(
                    "furniture definition given another sprite id: Turbo has one with the same",
                    $"'{item.Name}' sprite {entity.SpriteId} is now {free}"
                );
                entity.SpriteId = free;
                sprites.Add((free, entity.ProductType, entity.FurniCategory));
            }

            maxSprite[entity.ProductType] = Math.Max(
                maxSprite.GetValueOrDefault(entity.ProductType),
                entity.SpriteId
            );
            names.Add(entity.Name);

            var id = await ctx.AddSetupAsync(entity, TABLE, ct).ConfigureAwait(false);

            byName[(item.Type, item.Name.ToLowerInvariant())] = (
                id,
                entity.Name,
                entity.Logic,
                entity.FurniCategory
            );
            ctx.Definitions[item.Id] = new DefinitionRef(
                id,
                entity.Name,
                InteractionLogic.Effective(entity.Logic, entity.Name, keys),
                item.Type,
                entity.FurniCategory,
                item.Interaction
            );
        }
    }

    private static FurnitureDefinitionEntity Create(
        ImportContext ctx,
        BaseItem item,
        IReadOnlyDictionary<string, string> clothing,
        IReadOnlyDictionary<int, JsonObject> crackables
    )
    {
        var logic = InteractionLogic.For(item.Interaction, item.Type, ctx.Options.LogicKeys);
        var category = InteractionLogic.Category(item.Interaction, item.Name, item.Type);
        var row = item.Row;
        var sections = new JsonObject();

        var customParams = row.Text("customparams").Trim();

        if (logic.StartsWith("effect_", StringComparison.Ordinal))
        {
            // Turbo reads an effect furni's effect id from custom_params.
            var effect = row.Int("effect_id_male");

            if (effect <= 0)
                effect = row.Int("effect_id_female");

            if (effect > 0)
                customParams = effect.ToString(CultureInfo.InvariantCulture);
        }
        else if (logic == "purchasable_clothing" && clothing.TryGetValue(item.Name, out var sets))
            customParams = sets;

        if (logic == "vending_machine")
        {
            var handItems = new JsonArray();

            foreach (
                var part in row.Text("vending_ids")
                    .Split([',', ';'], StringSplitOptions.TrimEntries)
            )
                if (int.TryParse(part, out var handItem) && handItem > 0)
                    handItems.Add(handItem);

            if (handItems.Count > 0)
                sections["vending"] = new JsonObject { ["handItems"] = handItems };
        }

        if (logic == "crackable" && crackables.TryGetValue(item.Id, out var crackable))
            sections["crackable"] = crackable;

        return new FurnitureDefinitionEntity
        {
            SpriteId = item.Sprite,
            Name = item.Name,
            PublicName = Cut(row.String("public_name"), 255),
            ProductType = item.Type,
            FurniCategory = category,
            Logic = logic,
            TotalStates = Math.Max(0, row.Int("interaction_modes_count", 1)),
            Width = Math.Max(1, row.Int("width", 1)),
            Length = Math.Max(1, row.Int("length", 1)),
            StackHeight = Math.Max(0, row.Double("stack_height")),
            CanStack = row.Bool("allow_stack", true),
            CanWalk = row.Bool("allow_walk"),
            CanSit = row.Bool("allow_sit"),
            CanLay = row.Bool("allow_lay"),
            CanRecycle = row.Bool("allow_recycle"),
            CanTrade = row.Bool("allow_trade", true),
            CanGroup = row.Bool("allow_inventory_stack", true),
            CanSell = row.Bool("allow_marketplace_sell", true),
            UsagePolicy = FurnitureUsageType.Controller,
            CustomParams =
                customParams.Length == 0 ? null : Cut(customParams, CUSTOM_PARAMS_MAX_LENGTH),
            ExtraData = sections.Count == 0 ? null : sections.ToJsonString(),
        };
    }

    /// <summary>Every items_base row, read once: the catalog and the crackables name others by id.</summary>
    private static async Task<SortedDictionary<int, BaseItem>> ReadBasesAsync(
        ImportContext ctx,
        CancellationToken ct
    )
    {
        var bases = new SortedDictionary<int, BaseItem>();

        await foreach (
            var row in ctx.Source.ReadAsync("items_base", "id", ct).ConfigureAwait(false)
        )
        {
            var id = row.Int("id");

            bases[id] = new BaseItem(
                id,
                row.Text("item_name").Trim(),
                row.Int("sprite_id"),
                TypeOf(row.Text("type", "s")),
                row.Text("interaction_type", "default").Trim().ToLowerInvariant(),
                row.Detach()
            );
        }

        return bases;
    }

    /// <summary>catalog_clothing by the clothing furni's classname: the figure set ids it unlocks.</summary>
    private static async Task<Dictionary<string, string>> ReadClothingAsync(
        ImportContext ctx,
        CancellationToken ct
    )
    {
        var clothing = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        await foreach (
            var row in ctx.Source.ReadAsync("catalog_clothing", "id", ct).ConfigureAwait(false)
        )
            clothing.TryAdd(
                row.Text("name").Trim(),
                row.Text("setid").Replace(" ", "", StringComparison.Ordinal)
            );

        return clothing;
    }

    /// <summary>
    /// <c>items_crackable</c> as Turbo's definition section: the hits needed and each prize
    /// (<c>baseId:chance;...</c>) by classname, its chance its weight.
    /// </summary>
    private static async Task<Dictionary<int, JsonObject>> ReadCrackablesAsync(
        ImportContext ctx,
        IReadOnlyDictionary<int, BaseItem> bases,
        CancellationToken ct
    )
    {
        var crackables = new Dictionary<int, JsonObject>();

        await foreach (
            var row in ctx.Source.ReadAsync("items_crackable", "item_id", ct).ConfigureAwait(false)
        )
        {
            var rewards = new JsonArray();

            foreach (
                var prize in row.Text("prizes")
                    .Split(
                        ';',
                        StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
                    )
            )
            {
                var parts = prize.Split(':');

                if (
                    !int.TryParse(parts[0], out var baseId)
                    || !bases.TryGetValue(baseId, out var reward)
                )
                    continue;

                var weight =
                    parts.Length > 1 && int.TryParse(parts[1], out var chance) ? chance : 100;

                rewards.Add(
                    new JsonObject { ["furni"] = reward.Name, ["weight"] = Math.Max(1, weight) }
                );
            }

            if (rewards.Count == 0)
                continue;

            var section = new JsonObject
            {
                ["target"] = Math.Max(1, row.Int("count", 1)),
                ["rewards"] = rewards,
            };

            if (row.Int("required_effect") > 0)
                section["requiredEffectId"] = row.Int("required_effect");

            crackables[row.Int("item_id")] = section;
        }

        return crackables;
    }

    private static ProductType TypeOf(string letter) =>
        letter.Trim().ToLowerInvariant() switch
        {
            "i" => ProductType.Wall,
            "e" => ProductType.Effect,
            "b" => ProductType.Badge,
            "r" => ProductType.Robot,
            "h" => ProductType.HabboClub,
            "p" => ProductType.Pet,
            _ => ProductType.Floor,
        };

    private static string? Cut(string? value, int length) =>
        value is null ? null
        : value.Length > length ? value[..length]
        : value;
}

/// <summary>A Morningstar <c>items_base</c> row.</summary>
internal sealed record BaseItem(
    int Id,
    string Name,
    int Sprite,
    ProductType Type,
    string Interaction,
    SourceRow Row
);
