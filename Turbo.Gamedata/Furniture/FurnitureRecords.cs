using System.Collections.Generic;
using System.Text.Json.Nodes;
using Turbo.Database.Entities.Furniture;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Gamedata.Furniture;

/// <summary>
/// Definitions made from Habbo's items, and definitions as the JSON a change records: an object
/// of furnidata keys, so a change reads the way the file does.
/// </summary>
internal static class FurnitureRecords
{
    // The logic the room object provider falls back to (RoomObjectLogicProvider): a new item of
    // Habbo's behaves as plain furniture until staff give it its own.
    public const string DEFAULT_FLOOR_LOGIC = "default_floor";
    public const string DEFAULT_WALL_LOGIC = "default_wall";

    // A made definition's record carries what it was made as besides its fields, so it reads
    // whole in the history.
    public const string SPRITE_ID_KEY = "id";
    public const string CLASS_NAME_KEY = "classname";
    public const string LOGIC_KEY = "logic";

    /// <summary>A definition of one of Habbo's items the hotel does not have, with Habbo's fields.</summary>
    public static FurnitureDefinitionEntity Create(
        ProductType type,
        int spriteId,
        string className,
        JsonObject habbo
    )
    {
        var definition = new FurnitureDefinitionEntity
        {
            SpriteId = spriteId,
            Name = className,
            ProductType = type,
            FurniCategory = FurnitureCategory.Default,
            Logic = type == ProductType.Wall ? DEFAULT_WALL_LOGIC : DEFAULT_FLOOR_LOGIC,
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

        foreach (var field in FurnitureFields.All)
            if (field.AppliesTo(type == ProductType.Wall) && field.IsIn(habbo))
                field.Write(definition, habbo[field.Key]);

        return definition;
    }

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
