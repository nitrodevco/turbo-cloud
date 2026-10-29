using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.Json.Nodes;
using Turbo.Assets.Furniture;
using Turbo.Primitives.Furniture.Enums;

namespace Turbo.Gamedata.Furniture;

/// <summary>
/// A definition as a furniture bundle's own data says it would be. Only what the asset data holds
/// is read: whether it is a seat, a bed or a surface is not in it, and is left for staff to say.
/// </summary>
internal static class FurnitureDrafts
{
    // The client's logic of a behaviour the server has is "furniture_" and the server's key
    // (furniture_dice is dice, furniture_one_way_door is one_way_door).
    private const string CLIENT_LOGIC_PREFIX = "furniture_";

    // Client logics whose server key is spelt otherwise: the credit furni exchange themselves,
    // the dimmer is a room dimmer, the wheel and the random state keep their own names.
    private static readonly Dictionary<string, string> CLIENT_LOGIC_ALIASES = new(
        StringComparer.Ordinal
    )
    {
        ["furniture_credit"] = "exchange",
        ["furniture_roomdimmer"] = "dimmer",
        ["furniture_habbowheel"] = "habbo_wheel",
        ["furniture_randomstate"] = "random_state",
    };

    // Behaviours the client knows nothing of: a vending machine, a gate, a teleport and a roller
    // are furniture_multistate (or furniture_basic) to it, and only the furnidata's category
    // (its client category) says what they are. A live hotel (2026-10-29) has 206 vending
    // machines, 226 gates, 59 teleports and 14 rollers so marked.
    private static readonly Dictionary<string, string> CATEGORY_LOGICS = new(StringComparer.Ordinal)
    {
        ["vending_machine"] = "vending_machine",
        ["gate"] = "gate",
        ["teleport"] = "teleport",
        ["roller"] = "roller",
    };

    // Logics and visualizations the client gives only wall items: stickies, windows, posters and
    // the external image wall item.
    private static readonly HashSet<string> WALL_ONLY =
    [
        "furniture_stickie",
        "furniture_window",
        "furniture_poster",
        "furniture_external_image_wallitem",
    ];

    public static ProductType TypeOf(FurnitureAssetInfo info) =>
        WALL_ONLY.Contains(info.Logic ?? string.Empty)
        || WALL_ONLY.Contains(info.Visualization ?? string.Empty)
            ? ProductType.Wall
            : ProductType.Floor;

    /// <summary>
    /// The server logic it gets: the one registered under its classname, else the one of the
    /// client logic its data names when the server has it, else plain furniture.
    /// </summary>
    public static string LogicOf(
        ProductType type,
        string className,
        FurnitureAssetInfo info,
        IReadOnlySet<string> logics
    ) => LogicOf(type, className, info.Logic, clientCategory: null, logics);

    /// <summary>
    /// As the other, from the client logic its asset file names (<c>furniture_*</c>, or an alias
    /// of it the server spells otherwise) and, failing that, from the furnidata's client
    /// category: a <c>gate</c> or a <c>vending_machine</c> the client only knows as multistate
    /// furniture.
    /// </summary>
    public static string LogicOf(
        ProductType type,
        string className,
        string? clientLogic,
        string? clientCategory,
        IReadOnlySet<string> logics
    )
    {
        var logic = FurnitureRecords.LogicFor(type, className, logics);

        if (!FurnitureRecords.IsDefaultLogic(logic))
            return logic;

        if (clientLogic is { } client)
        {
            if (
                client.StartsWith(CLIENT_LOGIC_PREFIX, StringComparison.Ordinal)
                && logics.Contains(client[CLIENT_LOGIC_PREFIX.Length..])
            )
                return client[CLIENT_LOGIC_PREFIX.Length..];

            if (CLIENT_LOGIC_ALIASES.TryGetValue(client, out var alias) && logics.Contains(alias))
                return alias;
        }

        if (
            type == ProductType.Floor
            && clientCategory is { } category
            && CATEGORY_LOGICS.TryGetValue(category, out var byCategory)
            && logics.Contains(byCategory)
        )
            return byCategory;

        return logic;
    }

    /// <summary>The fields to make it with, by furnidata and server key.</summary>
    public static JsonObject Fields(
        string className,
        ProductType type,
        string logic,
        FurnitureAssetInfo info
    )
    {
        var fields = new JsonObject
        {
            [FurnitureRecords.CLASS_NAME_KEY] = className,
            [FurnitureRecords.LOGIC_KEY] = logic,
            ["name"] = className,
            ["description"] = string.Empty,
            ["states"] = info.States,
        };

        if (type == ProductType.Floor)
        {
            fields["xdim"] = Tiles(info.DimensionX);
            fields["ydim"] = Tiles(info.DimensionY);
            fields["height"] = Math.Round(Math.Max(0, info.DimensionZ ?? 0), 4);
        }

        return fields;
    }

    /// <summary>Its colours past the plain one (0): each a <c>classname*N</c>.</summary>
    public static ImmutableArray<int> Colors(FurnitureAssetInfo info) =>
        [.. info.Colors.Where(x => x > 0).Order()];

    private static int Tiles(double? size) =>
        size is { } value && !double.IsNaN(value) ? Math.Max(1, (int)Math.Round(value)) : 1;
}
