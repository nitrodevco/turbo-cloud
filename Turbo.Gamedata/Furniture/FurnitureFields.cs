using System;
using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Gamedata.Furniture;

/// <summary>
/// The fields a definition holds: the furnidata ones, which an import takes from Habbo and staff
/// may edit, and the server's own columns, which only staff set.
/// The built file has its own order, Habbo's (<see cref="FurnitureDataWriter"/>). The item's
/// identity (<c>id</c>, <c>classname</c>) is among the server's own: staff may change it, an
/// import matches by it and never writes it. Its offers (<c>offerid</c>, <c>buyout</c>,
/// <c>bc</c>, <c>bcofferid</c>, ...) are not fields at all: they are stamped from the catalog
/// whenever the file is built (<see cref="FurnitureOfferStamps"/>).
/// </summary>
internal static class FurnitureFields
{
    public static readonly ImmutableArray<FurnitureField> All =
    [
        FurnitureField.Int("revision", e => e.Revision, (e, v) => e.Revision = v, clientOnly: true),
        FurnitureField.Text("category", 64, e => e.ClientCategory, (e, v) => e.ClientCategory = v),
        FurnitureField.Int(
            "defaultdir",
            e => e.DefaultDirection,
            (e, v) => e.DefaultDirection = v,
            floorOnly: true,
            clientOnly: true
        ),
        FurnitureField.Int("xdim", e => e.Width, (e, v) => e.Width = v, floorOnly: true),
        FurnitureField.Int("ydim", e => e.Length, (e, v) => e.Length = v, floorOnly: true),
        FurnitureField.Colors("partcolors", e => e.PartColors, (e, v) => e.PartColors = v),
        FurnitureField.Text("name", 255, e => e.PublicName, (e, v) => e.PublicName = v),
        FurnitureField.Text("description", null, e => e.Description, (e, v) => e.Description = v),
        FurnitureField.Text("adurl", 512, e => e.AdUrl, (e, v) => e.AdUrl = v),
        FurnitureField.Bool(
            "excludeddynamic",
            e => e.ExcludedDynamic,
            (e, v) => e.ExcludedDynamic = v,
            clientOnly: true
        ),
        FurnitureField.Text(
            "customparams",
            512,
            e => e.CustomParams,
            (e, v) => e.CustomParams = v,
            floorOnly: true
        ),
        FurnitureField.Int(
            "specialtype",
            e => (int)e.FurniCategory,
            (e, v) => e.FurniCategory = (FurnitureCategory)v
        ),
        FurnitureField.Bool("canstandon", e => e.CanWalk, (e, v) => e.CanWalk = v, floorOnly: true),
        FurnitureField.Bool("cansiton", e => e.CanSit, (e, v) => e.CanSit = v, floorOnly: true),
        FurnitureField.Bool("canlayon", e => e.CanLay, (e, v) => e.CanLay = v, floorOnly: true),
        FurnitureField.Bool(
            "canputstuffon",
            e => e.CanStack,
            (e, v) => e.CanStack = v,
            floorOnly: true
        ),
        // stack_height is double(10,4), the four decimals Habbo's heights use (0.0001, 1.1125).
        FurnitureField.Double(
            "height",
            4,
            e => e.StackHeight,
            (e, v) => e.StackHeight = v,
            floorOnly: true
        ),
        FurnitureField.Text("furniline", 64, e => e.FurniLine, (e, v) => e.FurniLine = v),
        FurnitureField.Text("environment", 64, e => e.Environment, (e, v) => e.Environment = v),
        FurnitureField.Bool("rare", e => e.Rare, (e, v) => e.Rare = v, clientOnly: true),
        FurnitureField.Bool("tradeable", e => e.CanTrade, (e, v) => e.CanTrade = v),
        FurnitureField.Bool("recyclable", e => e.CanRecycle, (e, v) => e.CanRecycle = v),
        // Not furnidata's: read from the furniture's asset file (HabboFurnitureFiles).
        FurnitureField.Int(
            "states",
            e => e.TotalStates,
            (e, v) => e.TotalStates = v,
            fromFile: true
        ),
        // The server's own columns: neither furnidata nor Habbo's files have them, so an import
        // never touches them (FurnitureField.ServerOnly). The classname and sprite id are the
        // keys a made definition's record names them by (FurnitureRecords).
        FurnitureField.Text(
            FurnitureRecords.CLASS_NAME_KEY,
            null,
            e => e.Name,
            (e, v) => e.Name = v ?? string.Empty,
            serverOnly: true,
            check: v => Required(v, "classname")
        ),
        FurnitureField.Int(
            FurnitureRecords.SPRITE_ID_KEY,
            e => e.SpriteId,
            (e, v) => e.SpriteId = v,
            serverOnly: true,
            check: v => v?.GetValue<int>() > 0 ? null : "id must be above 0."
        ),
        FurnitureField.Text(
            FurnitureRecords.LOGIC_KEY,
            50,
            e => e.Logic,
            (e, v) => e.Logic = v ?? string.Empty,
            serverOnly: true,
            check: v => Required(v, "logic")
        ),
        FurnitureField.Int(
            "usagepolicy",
            e => (int)e.UsagePolicy,
            (e, v) => e.UsagePolicy = (FurnitureUsageType)v,
            serverOnly: true,
            check: v =>
                Enum.IsDefined((FurnitureUsageType)v!.GetValue<int>())
                    ? null
                    : "usagepolicy is 0 (nobody), 1 (rights holders) or 2 (everybody)."
        ),
        FurnitureField.Bool(
            "cangroup",
            e => e.CanGroup,
            (e, v) => e.CanGroup = v,
            serverOnly: true
        ),
        FurnitureField.Bool("cansell", e => e.CanSell, (e, v) => e.CanSell = v, serverOnly: true),
        FurnitureField.Text(
            "extradata",
            null,
            e => e.ExtraData,
            (e, v) => e.ExtraData = string.IsNullOrWhiteSpace(v) ? null : v,
            serverOnly: true,
            check: JsonObjectOrEmpty
        ),
    ];

    private static string? Required(JsonNode? value, string key) =>
        value?.GetValue<string>() is { } text && text.Trim().Length > 0 && text == text.Trim()
            ? null
            : $"{key} can't be empty or start or end with spaces.";

    /// <summary>Extra data is named JSON sections: an object, or nothing.</summary>
    private static string? JsonObjectOrEmpty(JsonNode? value)
    {
        var text = value?.GetValue<string>();

        if (string.IsNullOrWhiteSpace(text))
            return null;

        try
        {
            return JsonNode.Parse(text) is JsonObject ? null : "extradata must be a JSON object.";
        }
        catch (JsonException ex)
        {
            return $"extradata is not JSON: {ex.Message}";
        }
    }

    public static readonly FrozenDictionary<string, FurnitureField> ByKey = All.ToFrozenDictionary(
        x => x.Key
    );
}
