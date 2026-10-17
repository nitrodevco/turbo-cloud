using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Assets.Hab;
using Turbo.Primitives.Gamedata.Snapshots;
using Turbo.Primitives.Players;

namespace Turbo.Gamedata.ChatStyles;

/// <summary>
/// The chat styles of Habbo's HTML client, as nitro-studio reads them
/// (<c>server/habbo-client-data.ts</c>, <c>clientChatStyles</c>): <c>chatstyles_xml</c> in the
/// release's <c>generated/habbo-free-flow-chat-com.hab</c> lists them, each with its
/// <c>style_&lt;assetId&gt;_regpoints</c> and its bitmaps; the library's other bitmaps (its tray,
/// scrollbar) go into the bundle beside them.
/// </summary>
internal sealed partial class ChatStyleService
{
    public const string CLIENT_LIBRARY = "generated/habbo-free-flow-chat-com.hab";

    private static readonly string[] CLIENT_FLAGS =
    [
        "systemStyle",
        "purchasable",
        "hcOnly",
        "staffOverrideable",
        "allowHTML",
        "ambassadorOnly",
        "notification",
    ];

    public async Task<ChatStyleImportResult> ImportFromClientAsync(
        PlayerId player,
        CancellationToken ct
    )
    {
        var release =
            await client.OpenFollowedAsync(ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                "No release of Habbo's HTML client is downloaded: check the client on the Updates tab first."
            );
        var data =
            release.Read(CLIENT_LIBRARY)
            ?? throw new InvalidOperationException(
                $"Habbo's HTML client {release.Version} has no {CLIENT_LIBRARY}."
            );

        return await ApplyImportAsync(
                ReadClient(HabLibrary.Read(data, LIBRARY)),
                $"Habbo's HTML client {release.Version}",
                player,
                ct
            )
            .ConfigureAwait(false);
    }

    /// <summary>The client's chat styles, in <c>chatstyles_xml</c> order, and the library's other bitmaps.</summary>
    private static ReadBundleResult ReadClient(HabLibrary hab)
    {
        var xml =
            hab.GetXml("chatstyles_xml")
            ?? throw new ArgumentException("The chat library has no chatstyles_xml.");
        var styles = new List<ImportedStyle>();

        foreach (Match match in StyleTagPattern().Matches(CommentPattern().Replace(xml, "")))
        {
            var attributes = match.Groups[1].Value;
            var assetId = Attribute(attributes, "assetId");

            if (assetId is null || Text(hab, $"style_{assetId}_regpoints") is not { } regPoints)
                continue;

            if (
                !int.TryParse(Attribute(attributes, "id"), out var id)
                || id < 0
                || !AssetIdPattern().IsMatch(assetId)
            )
                continue;

            var bitmaps = new Dictionary<string, byte[]>(StringComparer.Ordinal);

            foreach (var file in REQUIRED_BITMAPS.Concat(OPTIONAL_BITMAPS))
            {
                if (hab.GetBinary($"style_{assetId}_{file}") is { } png)
                    bitmaps[file] = png;
            }

            var flags = new JsonObject();

            foreach (var flag in CLIENT_FLAGS.Where(x => Attribute(attributes, x) == "true"))
                flags[flag] = true;

            styles.Add(
                new ImportedStyle(
                    id,
                    assetId,
                    flags,
                    NormalizeRegPoints(RegPointsOf(regPoints).ToJsonString()),
                    bitmaps
                )
            );
        }

        // The library's own bitmaps: every picture but a style's, named as the bundle holds them.
        var library = new List<(string, byte[])>();

        foreach (
            var entry in hab.Entries.Where(x =>
                x.MimeType.StartsWith("image/", StringComparison.Ordinal)
            )
        )
        {
            var name = BitmapNamePattern().Replace(entry.Name, "");

            if (name.StartsWith("style_", StringComparison.Ordinal))
                continue;

            library.Add(($"{LIBRARY}-{name}", hab.Get(entry)));
        }

        return new ReadBundleResult(styles, [.. library.DistinctBy(x => x.Item1)]);
    }

    /// <summary>
    /// A style's regpoints file as <c>ChatStyleLibrary</c> reads it - only the keys the file has, in
    /// the order the Studio gives them - with <c>scale9</c> from <c>9sliceXY</c>/<c>9sliceWH</c>.
    /// </summary>
    internal static JsonObject RegPointsOf(string text)
    {
        static int? Int(string? value) => value is null ? null : ParseInt(value);

        var xy = ConfigCsv(text, "9sliceXY") ?? ["0", "0"];
        var wh = ConfigCsv(text, "9sliceWH") ?? ["0", "0"];
        var output = new JsonObject
        {
            ["scale9"] = Rect(
                xy.ElementAtOrDefault(0),
                xy.ElementAtOrDefault(1),
                wh.ElementAtOrDefault(0),
                wh.ElementAtOrDefault(1)
            ),
        };

        foreach (var key in new[] { "faceXY", "colorXY", "emblemXY", "emblemMultilineXY" })
        {
            if (
                Values(key) is { } found
                && Int(found.ElementAtOrDefault(0)) is { } x
                && Int(found.ElementAtOrDefault(1)) is { } y
            )
                output[key] = new JsonObject { ["x"] = x, ["y"] = y };
        }

        foreach (var key in new[] { "textFieldMargins", "overlapRect" })
        {
            if (
                Values(key) is { } found
                && Rect(
                    found.ElementAtOrDefault(0),
                    found.ElementAtOrDefault(1),
                    found.ElementAtOrDefault(2),
                    found.ElementAtOrDefault(3)
                )
                    is { } rect
            )
                output[key] = rect;
        }

        foreach (var key in new[] { "pointerY", "fontSize" })
        {
            if (Values(key) is { } found && Int(found[0]) is { } value)
                output[key] = value;
        }

        foreach (
            var key in new[]
            {
                "textColorRGB",
                "linkColorRGB",
                "linkHoverColorRGB",
                "linkActiveColorRGB",
            }
        )
        {
            if (Values(key) is { } found && FlashInt(found[0]) is { } value)
                output[key] = value;
        }

        foreach (var key in new[] { "anonymous", "usePixelPerfectNineSlice" })
        {
            if (Values(key) is { } found)
                output[key] = found[0] == "true";
        }

        if (
            Values("pointerXMargins") is { } margins
            && margins.Select(Int).ToList() is var ints
            && ints.All(x => x is not null)
        )
            output["pointerXMargins"] = new JsonArray([.. ints.Select(x => (JsonNode)x!.Value)]);

        if (Values("fontFace") is { } face)
            output["fontFace"] = face[0];

        return output;

        string[]? Values(string key) =>
            text.Contains(key, StringComparison.Ordinal) ? ConfigCsv(text, key) : null;

        JsonObject? Rect(string? x, string? y, string? width, string? height) =>
            Int(x) is { } rx && Int(y) is { } ry && Int(width) is { } rw && Int(height) is { } rh
                ? new JsonObject
                {
                    ["x"] = rx,
                    ["y"] = ry,
                    ["width"] = rw,
                    ["height"] = rh,
                }
                : null;
    }

    /// <summary>
    /// <c>getConfigCSV</c>: the first occurrence of the key anywhere in the text, and what follows its
    /// <c>=</c> (one space skipped) to the end of the line, split at commas.
    /// </summary>
    private static string[]? ConfigCsv(string text, string key)
    {
        var at = text.IndexOf(key, StringComparison.Ordinal);

        if (at < 0)
            return null;

        var equals = text.IndexOf('=', at);

        if (equals < 0)
            return null;

        var end = text.IndexOf("\r\n", equals, StringComparison.Ordinal);

        if (end < 0)
            end = text.IndexOf('\n', equals);

        if (end < 0)
            end = text.Length;

        var start = equals + (equals + 1 < text.Length && text[equals + 1] == ' ' ? 2 : 1);

        return start > end ? [""] : text[start..end].Split(',');
    }

    /// <summary>A Flash colour: hexadecimal after <c>0x</c>, else decimal.</summary>
    private static int? FlashInt(string value)
    {
        var trimmed = value.Trim();

        return trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? ParseInt(trimmed[2..], 16)
            : ParseInt(trimmed);
    }

    /// <summary>JavaScript's <c>parseInt</c>: leading space and a sign, then the digits there are; null for none.</summary>
    private static int? ParseInt(string text, int radix = 10)
    {
        var span = text.AsSpan().TrimStart();
        var negative = span.Length > 0 && span[0] == '-';

        if (span.Length > 0 && span[0] is '-' or '+')
            span = span[1..];

        if (radix == 10 && span.Length > 1 && span[0] == '0' && span[1] is 'x' or 'X')
        {
            radix = 16;
            span = span[2..];
        }

        long value = 0;
        var digits = 0;

        foreach (var c in span)
        {
            var digit = c switch
            {
                >= '0' and <= '9' => c - '0',
                >= 'a' and <= 'z' => c - 'a' + 10,
                >= 'A' and <= 'Z' => c - 'A' + 10,
                _ => 99,
            };

            if (digit >= radix || value > int.MaxValue)
                break;

            value = (value * radix) + digit;
            digits++;
        }

        return digits == 0 ? null : (int)(negative ? -value : value);
    }

    private static string? Text(HabLibrary hab, string name) =>
        hab.GetBinary(name) is { } data ? Encoding.UTF8.GetString(data) : null;

    private static string? Attribute(string attributes, string name)
    {
        var match = Regex.Match(attributes, $@"(?:^|\s){Regex.Escape(name)}\s*=\s*""([^""]*)""");

        return match.Success ? match.Groups[1].Value : null;
    }

    [GeneratedRegex(@"<style\s([^>]*?)/>")]
    private static partial Regex StyleTagPattern();

    [GeneratedRegex("<!--[\\s\\S]*?-->")]
    private static partial Regex CommentPattern();

    [GeneratedRegex("_(png|gif|jpe?g)$")]
    private static partial Regex BitmapNamePattern();
}
