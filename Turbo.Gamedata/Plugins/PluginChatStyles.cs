using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Gamedata.ChatStyles;
using Turbo.Primitives.Gamedata;
using Turbo.Primitives.Gamedata.Snapshots;
using Turbo.Primitives.Players;

namespace Turbo.Gamedata.Plugins;

/// <summary>
/// The chat bubbles a plugin brings as data (<c>docs/plugin-data.md</c>, "Chat bubbles"): its
/// <c>chatstyles/chatstyles.json</c>, a list of styles - <c>clientStyleId</c>, <c>assetId</c>, the
/// client's <c>regPoints</c> and who may speak with it (<c>clubOnly</c>, <c>staffOnly</c>, ...) - and
/// beside it a folder per style, named by its asset id, of the client's bitmaps
/// (<c>chat_bubble_base.png</c>, ...). As the plugin loads, each style whose client id and asset id
/// the hotel has neither is added through <see cref="IChatStyleService"/>, as the panel adds one, so
/// a theme needs no code for its bubbles and a style staff changed since is left as they made it.
/// </summary>
internal static class PluginChatStyles
{
    /// <summary>The folder in a plugin's own that holds its chat bubbles.</summary>
    public const string FOLDER = "chatstyles";

    /// <summary>The list of its styles, in <see cref="FOLDER"/>.</summary>
    public const string CATALOGUE = "chatstyles.json";

    /// <summary>The styles in <paramref name="folder"/>'s <see cref="FOLDER"/>; none when it has no list.</summary>
    public static IReadOnlyList<ChatStyleEdit> Read(string folder)
    {
        var path = Path.Combine(folder, FOLDER, CATALOGUE);

        if (!File.Exists(path))
            return [];

        var list =
            JsonNode.Parse(
                File.ReadAllText(path),
                documentOptions: new JsonDocumentOptions
                {
                    CommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true,
                }
            ) as JsonArray
            ?? throw new InvalidDataException(
                $"{FOLDER}/{CATALOGUE} is a list of styles, each with its clientStyleId, assetId and regPoints."
            );
        var styles = new List<ChatStyleEdit>();

        foreach (var entry in list.OfType<JsonObject>())
        {
            if (
                entry["clientStyleId"]?.GetValueKind() != JsonValueKind.Number
                || entry["assetId"]?.GetValueKind() != JsonValueKind.String
                || entry["regPoints"] is not JsonObject regPoints
            )
                throw new InvalidDataException(
                    $"Each style in {FOLDER}/{CATALOGUE} gives its clientStyleId (a number), assetId (its folder's name) and regPoints (the client's object): {entry.ToJsonString()}"
                );

            var assetId = entry["assetId"]!.GetValue<string>();
            var files = ChatStyleService
                .REQUIRED_BITMAPS.Concat(ChatStyleService.OPTIONAL_BITMAPS)
                .Select(name => (name, file: Path.Combine(folder, FOLDER, assetId, $"{name}.png")))
                .Where(x => File.Exists(x.file))
                .ToDictionary(x => x.name, x => (byte[]?)File.ReadAllBytes(x.file));
            bool flag(string name) => entry[name]?.GetValueKind() == JsonValueKind.True;

            styles.Add(
                new ChatStyleEdit
                {
                    ClientStyleId = entry["clientStyleId"]!.GetValue<int>(),
                    AssetId = assetId,
                    ClubOnly = flag("clubOnly"),
                    AmbassadorOnly = flag("ambassadorOnly"),
                    StaffOnly = flag("staffOnly"),
                    Purchasable = flag("purchasable"),
                    System = false,
                    StaffOverrideable = flag("staffOverrideable"),
                    AllowHtml = flag("allowHtml"),
                    Notification = flag("notification"),
                    RegPoints = regPoints.ToJsonString(),
                    Files = files,
                }
            );
        }

        return styles;
    }

    /// <summary>Adds each of <paramref name="styles"/> the hotel has neither the client id nor the asset id of; the client ids added.</summary>
    public static async Task<IReadOnlyList<int>> SeedAsync(
        IChatStyleService chatStyles,
        IReadOnlyList<ChatStyleEdit> styles,
        CancellationToken ct
    )
    {
        if (styles.Count == 0)
            return [];

        var hotel = await chatStyles.ListAsync(ct).ConfigureAwait(false);
        var clientIds = hotel.Styles.Select(x => x.ClientStyleId).ToHashSet();
        var assetIds = hotel
            .Styles.Select(x => x.AssetId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var added = new List<int>();

        foreach (var style in styles)
        {
            if (clientIds.Contains(style.ClientStyleId!.Value) || assetIds.Contains(style.AssetId!))
                continue;

            await chatStyles.SaveAsync(0, style, new PlayerId(0), ct).ConfigureAwait(false);
            clientIds.Add(style.ClientStyleId.Value);
            assetIds.Add(style.AssetId!);
            added.Add(style.ClientStyleId.Value);
        }

        return added;
    }
}
