using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using Turbo.Database.Entities.Furniture;
using Turbo.Database.Furniture.Defaults;
using Turbo.Gamedata.Habbo;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.ExtraData;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Gamedata.Furniture;

/// <summary>
/// Definitions made from Habbo's items, and definitions as the JSON a change records: an object
/// of furnidata keys, so a change reads the way the file does.
/// </summary>
internal static class FurnitureRecords
{
    private static readonly FrozenDictionary<string, (int Usage, string Vending)> VENDING =
        FurnitureVendingDefaults20261008.Entries.ToFrozenDictionary(
            x => x.Name,
            x => (x.Usage, x.Vending),
            StringComparer.Ordinal
        );

    // The logic the room object provider falls back to (RoomObjectLogicProvider): a new item of
    // Habbo's behaves as plain furniture until staff give it its own.
    public const string DEFAULT_FLOOR_LOGIC = "default_floor";
    public const string DEFAULT_WALL_LOGIC = "default_wall";

    // Colour variants are named name*N (wf_link*2); they share their base item's logic.
    private const char COLOUR_VARIANT_MARK = '*';

    // A made definition's record carries what it was made as besides its fields, so it reads
    // whole in the history.
    public const string SPRITE_ID_KEY = "id";
    public const string CLASS_NAME_KEY = "classname";
    public const string LOGIC_KEY = "logic";

    /// <summary>Whether it is the plain logic every floor or wall item gets without one of its own.</summary>
    public static bool IsDefaultLogic(string logic) =>
        logic is DEFAULT_FLOOR_LOGIC or DEFAULT_WALL_LOGIC;

    /// <summary>
    /// The logic a new definition of Habbo's item gets: the one the server registers under its
    /// classname (the six WIRED Links boxes of 2026-10, <c>wf_link</c> and the rest, each have
    /// theirs), colour variants that of their base item, otherwise the default, as plain
    /// furniture until staff give it its own.
    /// </summary>
    public static string LogicFor(ProductType type, string className, IReadOnlySet<string> logics)
    {
        var mark = className.IndexOf(COLOUR_VARIANT_MARK, StringComparison.Ordinal);
        var baseName = mark > 0 ? className[..mark] : className;

        if (logics.Contains(baseName))
            return baseName;

        return type == ProductType.Wall ? DEFAULT_WALL_LOGIC : DEFAULT_FLOOR_LOGIC;
    }

    /// <summary>A definition of one of Habbo's items the hotel does not have, with Habbo's fields.</summary>
    public static FurnitureDefinitionEntity Create(
        ProductType type,
        int spriteId,
        string className,
        string logic,
        JsonObject habbo
    )
    {
        var definition = Blank(type, spriteId, className, logic);

        // A fresh database runs the data migration before Habbo's first import has any rows.
        // Only newly created vending definitions receive the same frozen defaults here.
        if (
            type == ProductType.Floor
            && logic == "vending_machine"
            && VENDING.TryGetValue(HabboFurnitureFiles.AssetName(className), out var vending)
        )
        {
            definition.UsagePolicy = (FurnitureUsageType)vending.Usage;
            definition.ExtraData = new JsonObject
            {
                [VendingMachineData.SECTION] = JsonNode.Parse(vending.Vending),
            }.ToJsonString();
        }

        foreach (var field in FurnitureFields.All)
            if (field.AppliesTo(type == ProductType.Wall) && field.IsIn(habbo))
                field.Write(definition, habbo[field.Key]);

        return definition;
    }

    /// <summary>A definition of plain furniture, before any field is given.</summary>
    public static FurnitureDefinitionEntity Blank(
        ProductType type,
        int spriteId,
        string className,
        string logic
    ) =>
        new()
        {
            SpriteId = spriteId,
            Name = className,
            ProductType = type,
            FurniCategory = FurnitureCategory.Default,
            Logic = logic,
            Width = 1,
            Length = 1,
            StackHeight = 0,
            CanStack = false,
            CanWalk = false,
            CanSit = false,
            CanLay = false,
            CanRecycle = false,
            CanTrade = true,
            CanGroup = true,
            CanSell = true,
            UsagePolicy = FurnitureUsageType.Controller,
        };

    /// <summary>The definition's values of these fields.</summary>
    public static JsonObject Fields(FurnitureDefinitionEntity definition, IEnumerable<string> keys)
    {
        var record = new JsonObject();

        foreach (var key in keys)
            record[key] = FurnitureFields.ByKey[key].Read(definition);

        return record;
    }

    /// <summary>A definition made, whole: what it was made as, and each of its fields.</summary>
    public static JsonObject Created(FurnitureDefinitionEntity definition)
    {
        var record = new JsonObject
        {
            [SPRITE_ID_KEY] = definition.SpriteId,
            [CLASS_NAME_KEY] = definition.Name,
            [LOGIC_KEY] = definition.Logic,
        };

        foreach (var field in FurnitureFields.All)
            if (field.AppliesTo(definition.ProductType == ProductType.Wall))
                record[field.Key] = field.Read(definition);

        return record;
    }
}
