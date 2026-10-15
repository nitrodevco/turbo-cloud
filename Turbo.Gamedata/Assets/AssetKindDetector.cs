using System;
using System.Linq;
using System.Text.Json.Nodes;
using Turbo.Primitives.Gamedata.Enums;

namespace Turbo.Gamedata.Assets;

/// <summary>
/// The kind of bundle a library's asset data (its <c>&lt;name&gt;.json</c>, as the converter writes
/// it) is, by what only that kind carries - the rules the studio's converter goes by:
/// <list type="bullet">
/// <item>a pet's index names the <c>pet</c> logic or a <c>pet_...</c> visualization;</item>
/// <item>any other logic or visualization is furniture's;</item>
/// <item>an effect has its own animation (<c>animations</c>, beside the assets);</item>
/// <item>clothing draws avatar parts: its assets are named <c>h_...</c> (<c>h_std_ch_3030_0_0</c>).</item>
/// </list>
/// Anything else (a generic room library, an empty one) is no kind of bundle the hotel keeps.
/// </summary>
internal static class AssetKindDetector
{
    private const string PET = "pet";
    private const string AVATAR_ASSET_PREFIX = "h_";

    public static AssetBundleKind? Of(JsonObject? data)
    {
        if (data is null)
            return null;

        var logic = Text(data["logicType"]);
        var visualization = Text(data["visualizationType"]);

        if (logic == PET || visualization?.StartsWith(PET, StringComparison.Ordinal) == true)
            return AssetBundleKind.Pet;

        if (logic is not null || visualization is not null)
            return AssetBundleKind.Furniture;

        if (data["animations"] is JsonArray { Count: > 0 })
            return AssetBundleKind.Effect;

        return AssetNames(data)
            .Any(x => x.StartsWith(AVATAR_ASSET_PREFIX, StringComparison.Ordinal))
            ? AssetBundleKind.Figure
            : null;
    }

    /// <summary>Its assets' names, whether kept as a list (as converted here) or by name (as older bundles keep them).</summary>
    private static string[] AssetNames(JsonObject data) =>
        data["assets"] switch
        {
            JsonArray list =>
            [
                .. list.OfType<JsonObject>().Select(x => Text(x["name"])).OfType<string>(),
            ],
            JsonObject byName => [.. byName.Select(x => x.Key)],
            _ => [],
        };

    private static string? Text(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var text) && text.Length > 0
            ? text
            : null;
}
