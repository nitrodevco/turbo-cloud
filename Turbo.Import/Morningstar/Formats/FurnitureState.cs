using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json.Nodes;
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Texts;

namespace Turbo.Import.Morningstar.Formats;

/// <summary>What a Morningstar item says of itself, for <see cref="FurnitureState"/>.</summary>
internal sealed record ItemState
{
    /// <summary><c>items.extra_data</c>, as stored.</summary>
    public string ExtraData { get; init; } = string.Empty;

    /// <summary><c>items.limited_data</c>: <c>seriesSize:serialNumber</c>.</summary>
    public string LimitedData { get; init; } = string.Empty;

    /// <summary>The Turbo group of a group furni, or 0.</summary>
    public int GuildId { get; init; }

    /// <summary>The other half of a teleporter pair, or 0.</summary>
    public int Partner { get; init; }

    /// <summary>Its room's <c>moodlight_data</c>, for a dimmer.</summary>
    public string? RoomMoodlight { get; init; }

    /// <summary>A present's wrapped furni's classname, for the card.</summary>
    public string? PresentProduct { get; init; }

    /// <summary>Turbo's song for a Morningstar soundtrack id, for a music disc.</summary>
    public Func<int, int?> SongOf { get; init; } = _ => null;

    /// <summary>A player's name by id, for a rentable space's renter.</summary>
    public Func<int, string?> NameOf { get; init; } = _ => null;

    public DateTime Now { get; init; }
}

/// <summary>
/// A Morningstar item's state as Turbo's <c>extra_data</c>: a JSON object of sections, whose
/// <c>stuff</c> is what the client is shown, in the shape the item's Turbo logic reads (a string,
/// a map, a list of strings or of numbers). Morningstar keeps one delimited string per interaction
/// (a trophy's <c>owner\tD-M-YYYY\ttext</c>, a mannequin's <c>gender:figure:name</c>...);
/// each is read here and written as Turbo's logic writes it. Null means the item needs nothing
/// stored: Turbo's default for its logic is what it would show.
/// </summary>
internal static class FurnitureState
{
    public static JsonObject? Translate(DefinitionRef def, ItemState item)
    {
        var sections = new JsonObject();
        var raw = item.ExtraData;
        JsonNode? data = null;

        switch (def.Logic)
        {
            case "teleport":
                data = "0";

                if (item.Partner > 0)
                    sections["room_linker"] = new JsonObject { ["ItemId"] = item.Partner };

                break;
            case "dimmer":
                data = Dimmer(raw);

                if (Presets(item.RoomMoodlight) is { Count: > 0 } presets)
                    sections["dimmer"] = presets;

                break;
            case "stickie":
            {
                var (color, text) = StickieColors.Split(raw.Trim());

                data = StickieColors.Compose(
                    StickieColors.IsValid(color) ? color.ToUpperInvariant() : StickieColors.DEFAULT,
                    text.Replace("\t", string.Empty, StringComparison.Ordinal)
                );
                break;
            }
            case "trophy":
            {
                var parts = raw.Split('\t');

                data =
                    parts.Length >= 3
                        ? TrophyData.Compose(
                            parts[0],
                            Date(parts[1], item.Now),
                            string.Join(' ', parts.Skip(2))
                        )
                        : raw;
                break;
            }
            case "badge_display":
            {
                // Morningstar: owner, date, badge. Turbo: state, badge, owner, date.
                var parts = raw.Split('\t');

                data =
                    parts.Length >= 3
                        ? Strings(
                            BadgeDisplayData.Compose(parts[2], parts[0], Date(parts[1], item.Now))
                        )
                        : Strings(BadgeDisplayData.Compose(raw, string.Empty, string.Empty));
                break;
            }
            case "mannequin":
            {
                var parts = raw.Split(':');

                data = new JsonObject
                {
                    [MannequinData.GENDER] =
                        parts.Length > 0
                        && parts[0].Trim().Equals("f", StringComparison.OrdinalIgnoreCase)
                            ? "F"
                            : "M",
                    [MannequinData.FIGURE] =
                        parts.Length > 1 ? MannequinData.ExtractClothing(parts[1]) : string.Empty,
                    [MannequinData.OUTFIT_NAME] =
                        parts.Length > 2 ? string.Join(':', parts.Skip(2)) : string.Empty,
                };
                break;
            }
            case "background_toner":
            {
                var parts = raw.Split(':');

                data =
                    parts.Length == 4 && parts.All(x => int.TryParse(x, out _))
                        ? Numbers(
                            parts.Select(x =>
                                Math.Clamp(int.Parse(x, CultureInfo.InvariantCulture), 0, 255)
                            )
                        )
                        : Numbers([0, 126, 126, 126]);
                break;
            }
            case "room_branding":
                data = Branding(raw);
                break;
            case "love_lock":
            {
                var parts = raw.Split('\t');

                data =
                    parts.Length == 6
                        ? Strings([
                            FriendFurniData.LOCKED,
                            parts[1],
                            parts[2],
                            parts[3],
                            parts[4],
                            Date(parts[5], item.Now),
                        ])
                        : Strings([FriendFurniData.UNLOCKED, "", "", "", "", ""]);
                break;
            }
            case "present":
                data = Present(raw, item.PresentProduct, out var box, out var ribbon);
                sections["gift"] = new JsonObject { ["BoxType"] = box, ["RibbonType"] = ribbon };
                break;
            case "crackable":
                sections["stuff"] = new JsonObject
                {
                    ["Data"] = "0",
                    ["Hits"] = Math.Max(0, Int(raw)),
                    ["Target"] = 0,
                };
                break;
            case "guild_customized" or "guild_forum":
                data = Strings([
                    Int(raw).ToString(CultureInfo.InvariantCulture),
                    item.GuildId.ToString(CultureInfo.InvariantCulture),
                    "",
                    "",
                    "",
                ]);
                break;
            case "song_disk":
            {
                // Owner, day, month, year, length, name, song id: one a line.
                var song = raw.Split('\n').LastOrDefault()?.Trim() ?? string.Empty;

                data =
                    int.TryParse(song, out var songId) && item.SongOf(songId) is { } turboSong
                        ? turboSong.ToString(CultureInfo.InvariantCulture)
                        : "0";
                break;
            }
            case "game_timer"
            or "wf_upcounter1"
            or "wf_upcounter2"
            or "wf_game_upcounter1"
            or "wf_game_upcounter2":
            {
                // now\tbase, in seconds; an older single number is the base.
                var parts = raw.Split('\t');

                data = Math.Max(0, Int(parts.Length > 1 ? parts[1] : parts[0]))
                    .ToString(CultureInfo.InvariantCulture);
                break;
            }
            case "monsterplant_seed":
                if (Int(raw) > 0)
                    sections["monsterplant_seed"] = new JsonObject
                    {
                        ["MinRarityLevel"] = Int(raw),
                    };

                break;
            case "rentable_space":
            {
                var parts = raw.Split(':');

                if (
                    parts.Length == 2
                    && int.TryParse(parts[0], out var renter)
                    && long.TryParse(parts[1], out var ends)
                    && renter > 0
                    && ends > new DateTimeOffset(item.Now).ToUnixTimeSeconds()
                    && item.NameOf(renter) is { } name
                )
                    sections["rentable_space"] = new JsonObject
                    {
                        ["RenterId"] = renter,
                        ["RenterName"] = name,
                        ["ExpiresAt"] = ends,
                    };

                break;
            }
            case "dice":
                data = Int(raw) < 0 ? "0" : State(raw);
                break;
            default:
                if (def.Logic.StartsWith("wf_", StringComparison.Ordinal) && def.Logic != "wf_link")
                    break; // A wired box's state is the room's, never stored.

                data =
                    def.Category
                        is FurnitureCategory.WallPaper
                            or FurnitureCategory.Floor
                            or FurnitureCategory.Landscape
                            or FurnitureCategory.Poster
                    // A product value ("101", "1.1"), or a photo's JSON: kept as it is.
                    || raw.TrimStart().StartsWith('{')
                        ? raw
                        : State(raw);
                break;
        }

        if (data is not null)
            sections["stuff"] = new JsonObject { ["Data"] = data };

        Limited(sections, item.LimitedData);

        return sections.Count == 0 ? null : sections;
    }

    /// <summary>
    /// An LTD's serial: Morningstar's <c>seriesSize:serialNumber</c> as Turbo's unique number and
    /// series, beside whatever the stuff holds.
    /// </summary>
    private static void Limited(JsonObject sections, string limited)
    {
        var parts = limited.Split(':');

        if (parts.Length != 2 || Int(parts[0]) <= 0 || Int(parts[1]) <= 0)
            return;

        if (sections["stuff"] is not JsonObject stuff)
            sections["stuff"] = stuff = new JsonObject { ["Data"] = "0" };

        stuff["UniqueNumber"] = Int(parts[1]);
        stuff["UniqueSeries"] = Int(parts[0]);
    }

    /// <summary>A plain state: a whole number, else 0 (Morningstar reads empty as 0 too).</summary>
    private static string State(string raw) =>
        int.TryParse(raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var state)
        && state >= 0
            ? state.ToString(CultureInfo.InvariantCulture)
            : "0";

    /// <summary>
    /// The dimmer's own line: both emulators write <c>on,preset,background,#RRGGBB,brightness</c>
    /// with 2 for yes and 1 for no.
    /// </summary>
    private static string Dimmer(string raw) =>
        Preset(raw) is { } preset
            ? string.Join(
                DimmerStates.SEPARATOR,
                preset.Enabled ? DimmerStates.ON : DimmerStates.OFF,
                preset.Id,
                preset.Type,
                preset.Color,
                preset.Brightness
            )
            : $"{DimmerStates.OFF},1,{DimmerStates.EFFECT_ROOM},{DimmerStates.DEFAULT_COLOR},{DimmerStates.MAX_BRIGHTNESS}";

    /// <summary>The three presets Morningstar keeps on the room, as Turbo's dimmer section.</summary>
    private static JsonArray? Presets(string? moodlight)
    {
        if (string.IsNullOrWhiteSpace(moodlight))
            return null;

        var presets = new JsonArray();
        var id = 1;

        foreach (var entry in moodlight.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            if (id > DimmerStates.PRESET_COUNT)
                break;

            if (Preset(entry) is not { } preset)
                continue;

            presets.Add(
                new JsonObject
                {
                    ["id"] = id++,
                    ["type"] = preset.Type,
                    ["color"] = preset.Color,
                    ["brightness"] = preset.Brightness,
                }
            );
        }

        return presets;
    }

    private static (bool Enabled, int Id, int Type, string Color, int Brightness)? Preset(
        string raw
    )
    {
        var parts = raw.Trim().Split(DimmerStates.SEPARATOR);

        if (parts.Length != DimmerStates.FIELD_COUNT)
            return null;

        var color = parts[3].Trim().ToUpperInvariant();

        return (
            Int(parts[0]) == DimmerStates.ON,
            Math.Clamp(Int(parts[1]), 1, DimmerStates.PRESET_COUNT),
            Int(parts[2]) == DimmerStates.EFFECT_BACKGROUND
                ? DimmerStates.EFFECT_BACKGROUND
                : DimmerStates.EFFECT_ROOM,
            DimmerStates.IsValidColor(color) ? color : DimmerStates.DEFAULT_COLOR,
            Math.Clamp(Int(parts[4]), DimmerStates.MIN_BRIGHTNESS, DimmerStates.MAX_BRIGHTNESS)
        );
    }

    /// <summary>
    /// Room ads: Morningstar's <c>key=value;</c> pairs. A value is split from its key at the first
    /// <c>=</c> only, so a link with a query string survives, which Morningstar's own reader lost.
    /// </summary>
    private static JsonObject Branding(string raw)
    {
        var map = new JsonObject
        {
            ["state"] = "0",
            ["imageUrl"] = "",
            ["clickUrl"] = "",
            ["offsetX"] = "0",
            ["offsetY"] = "0",
            ["offsetZ"] = "0",
        };

        foreach (var pair in raw.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var split = pair.IndexOf('=', StringComparison.Ordinal);

            if (split <= 0)
                continue;

            var key = pair[..split].Trim();

            if (map.ContainsKey(key))
                map[key] = pair[(split + 1)..].Trim();
        }

        return map;
    }

    /// <summary>
    /// A present's card. Morningstar: <c>N\tid1..idN\tcolour\tribbon\tshowSender\tmessage\tname\tlook</c>;
    /// its colour and ribbon are the client's box and ribbon, as Turbo's are.
    /// </summary>
    private static JsonObject Present(string raw, string? product, out int box, out int ribbon)
    {
        var parts = raw.Split('\t');
        var count = parts.Length > 0 ? Math.Max(0, Int(parts[0])) : 0;
        var at = 1 + count;

        string Part(int i) => at + i < parts.Length ? parts[at + i] : string.Empty;

        box = Int(Part(0));
        ribbon = Int(Part(1));

        if (box is < 0 or > 8 or 7)
            box = 0;

        if (ribbon is < 0 or > 10)
            ribbon = 0;

        var card = new JsonObject
        {
            [PresentData.MESSAGE] = Part(3),
            [PresentData.PRODUCT_CODE] = product ?? string.Empty,
            [PresentData.EXTRA_PARAM] = string.Empty,
        };

        if (Part(2) == "1" && Part(4).Length > 0)
        {
            card[PresentData.PURCHASER_NAME] = Part(4);
            card[PresentData.PURCHASER_FIGURE] = Part(5);
        }

        return card;
    }

    /// <summary>The wrapped items' ids in a present's extra data.</summary>
    public static IEnumerable<int> PresentContents(string raw)
    {
        var parts = raw.Split('\t');

        if (parts.Length < 5 || !int.TryParse(parts[0], out var count))
            yield break;

        for (var i = 1; i <= count && i < parts.Length; i++)
            if (int.TryParse(parts[i], out var id) && id > 0)
                yield return id;
    }

    /// <summary>Morningstar's <c>D-M-YYYY</c> as the client's <c>dd-MM-yyyy</c>.</summary>
    private static string Date(string value, DateTime fallback)
    {
        var parts = value.Trim().Split('-');

        if (
            parts.Length == 3
            && int.TryParse(parts[0], out var day)
            && int.TryParse(parts[1], out var month)
            && int.TryParse(parts[2], out var year)
            && year is >= 1 and <= 9999
            && month is >= 1 and <= 12
            && day >= 1
            && day <= DateTime.DaysInMonth(year, month)
        )
            return ClientDates.Format(new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Utc));

        return ClientDates.Format(fallback);
    }

    private static JsonArray Strings(IEnumerable<string> values) =>
        [.. values.Select(x => (JsonNode)x)];

    private static JsonArray Numbers(IEnumerable<int> values) =>
        [.. values.Select(x => (JsonNode)x)];

    private static int Int(string? value) =>
        int.TryParse(
            value?.Trim(),
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var parsed
        )
            ? parsed
            : 0;
}
