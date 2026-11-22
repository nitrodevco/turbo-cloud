using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Turbo.Import.Morningstar.Formats;

/// <summary>A furni a Morningstar match-snapshot box remembered: what it is to be put back to.</summary>
internal sealed record SnapshotPick(int ItemId, string State, int Rotation, int X, int Y);

/// <summary>A wired box's settings in Turbo's shape, and what could not be carried over.</summary>
internal sealed record WiredResult(
    JsonObject Wired,
    IReadOnlyList<SnapshotPick> Snapshot,
    string? Lost
);

/// <summary>
/// A Morningstar wired box's settings (<c>items.wired_data</c>) as Turbo's <c>wired</c> section.
/// Morningstar 3.x writes JSON (keys as its classes name their fields) and reads an older
/// delimited form; both are read here. Turbo's boxes carry Habbo's names, so the box is known by
/// its Morningstar interaction; each is written with every int its Turbo twin's rules need, inside
/// those rules, because one int out of range resets them all when the room loads.
/// <para>
/// Units: an action's delay is 500 ms pulses in both. Morningstar keeps a trigger's period in
/// milliseconds; Turbo in pulses (500 ms) or, for the long ones, 5 s units. Team colours are
/// Morningstar's enum names; Turbo's numbers are the client's, 1 red to 4 yellow.
/// </para>
/// </summary>
internal static class WiredSettings
{
    private const int MAX_PICKS = 20;
    private const int MAX_DELAY_PULSES = 120;
    private const int DEFAULT_BUBBLE = 202;

    private static readonly string[] TEAMS = ["NONE", "RED", "GREEN", "BLUE", "YELLOW"];

    private static readonly string[] DIRECTIONS =
    [
        "NORTH",
        "NORTH_EAST",
        "EAST",
        "SOUTH_EAST",
        "SOUTH",
        "SOUTH_WEST",
        "WEST",
        "NORTH_WEST",
    ];

    /// <summary>
    /// Turbo's settings for a box, or null for one Turbo has no twin of. <paramref name="classnameOf"/>
    /// names an <c>items_base</c> id, for a reward box's furni prizes.
    /// </summary>
    public static WiredResult? Translate(
        string interaction,
        string? wiredData,
        Func<int, string?> classnameOf
    )
    {
        var source = MorningstarWired.Parse(wiredData);
        var box = new Box();
        string? lost = null;

        switch (interaction)
        {
            // Triggers
            case "wf_trg_walks_on_furni" or "wf_trg_walks_off_furni" or "wf_trg_state_changed":
                box.Picks(source.ItemIds(legacyField: 2, legacySeparator: ':'));
                break;
            case "wf_trg_enter_room":
                box.Text = source.Json?["username"]?.GetValue<string>() ?? source.Raw;
                break;
            case "wf_trg_says_something":
            {
                var ownerOnly = source.Json is { } json
                    ? json["ownerOnly"]?.GetValue<bool>() ?? false
                    : source.Field('\t', 0) == "1";

                box.Ints(ownerOnly ? 1 : 0, 0, 1);
                box.Text = source.Json?["key"]?.GetValue<string>()
                    ?? source.Field('\t', 1)
                    ?? string.Empty;
                break;
            }
            case "wf_trg_periodically":
                box.Ints(Math.Max(1, source.Number("repeatTime", 10_000) / 500));
                break;
            case "wf_trg_period_long":
                box.Ints(Math.Clamp(source.Number("repeatTime", 100_000) / 5_000, 1, 429_496));
                break;
            case "wf_trg_at_given_time":
                box.Ints(Math.Max(0, source.Number("executeTime", 10_000) / 500));
                break;
            case "wf_trg_at_time_long":
                box.Ints(Math.Clamp(source.Number("executeTime", 100_000) / 5_000, 1, 429_496));
                break;
            case "wf_trg_bot_reached_stf":
                box.Text = source.Json?["botName"]?.GetValue<string>()
                    ?? source.Field(':', 0)
                    ?? string.Empty;
                box.Picks(source.ItemIds(legacyField: 1, legacySeparator: ':'));
                break;
            case "wf_trg_bot_reached_avtr":
                box.Text = source.Json?["botName"]?.GetValue<string>() ?? source.Raw;
                break;
            case "wf_trg_score_achieved":
                box.Ints(Math.Clamp(source.Number("score", 100), 1, 1000), 0);
                break;
            case "wf_trg_collision" or "wf_trg_game_starts" or "wf_trg_game_ends":
                break;

            // Actions
            case "wf_act_toggle_state"
            or "wf_act_teleport_to":
                box.Action(source.Delay('\t', 0));
                box.Picks(source.ItemIds(legacyField: 1, legacySeparator: '\t'));
                box.Ints(0);
                break;
            case "wf_act_toggle_to_rnd" or "wf_act_call_stacks" or "wf_act_chase" or "wf_act_flee":
                box.Action(source.Delay('\t', 0));
                box.Picks(source.ItemIds(legacyField: 1, legacySeparator: '\t'));
                break;
            case "wf_act_reset_timers" or "wf_act_leave_team":
                box.Action(
                    source.Json is null ? source.Int(source.Raw) : source.Number("delay", 0)
                );
                break;
            case "wf_act_join_team":
                box.Action(source.Delay('\t', 0));
                box.Ints(Math.Clamp(source.Team("team", '\t', 1), 1, 4), 0);
                break;
            case "wf_act_give_score":
                box.Action(
                    source.Json is null
                        ? source.Int(source.Field(';', 2))
                        : source.Number("delay", 0)
                );
                box.Ints(
                    Math.Clamp(
                        source.Json is null
                            ? source.Int(source.Field(';', 0))
                            : source.Number("score", 5),
                        -1000,
                        1000
                    ),
                    Math.Clamp(
                        source.Json is null
                            ? source.Int(source.Field(';', 1))
                            : source.Number("count", 1),
                        0,
                        10
                    )
                );
                break;
            case "wf_act_give_score_tm":
                box.Action(
                    source.Json is null
                        ? source.Int(source.Field(';', 3))
                        : source.Number("delay", 0)
                );
                box.Ints(
                    Math.Clamp(
                        source.Json is null
                            ? source.Int(source.Field(';', 0))
                            : source.Number("score", 5),
                        -1000,
                        1000
                    ),
                    Math.Clamp(
                        source.Json is null
                            ? source.Int(source.Field(';', 1))
                            : source.Number("count", 1),
                        0,
                        10
                    ),
                    Math.Clamp(
                        source.Json is null
                            ? source.Int(source.Field(';', 2))
                            : Team(source.Json["team"]),
                        1,
                        4
                    )
                );
                break;
            case "wf_act_show_message":
                box.Action(source.Delay('\t', 0));
                box.Text = source.Message(1);
                box.Ints(0, DEFAULT_BUBBLE, -1);
                break;
            case "wf_act_give_effect":
                box.Action(source.Delay('\t', 0));
                box.Ints(Math.Clamp(source.Int(source.Message(1)), 0, 10_000), 2, 0);
                break;
            case "wf_act_kick_user":
                box.Action(source.Delay('\t', 0));
                box.Text = source.Message(1);
                break;
            case "wf_act_mute_triggerer":
                box.Action(source.Delay('\t', 0));
                box.Ints(
                    Math.Clamp(
                        source.Json is null
                            ? source.Int(source.Field('\t', 1))
                            : source.Number("length", 1),
                        0,
                        10
                    )
                );
                box.Text = source.Json?["message"]?.GetValue<string>()
                    ?? source.Field('\t', 2)
                    ?? string.Empty;
                break;
            case "wf_act_move_rotate":
                box.Action(
                    source.Json is null
                        ? source.Int(source.Field('\t', 2))
                        : source.Number("delay", 0)
                );
                box.Ints(
                    Math.Clamp(
                        source.Json is null
                            ? source.Int(source.Field('\t', 0))
                            : source.Number("direction", 0),
                        0,
                        11
                    ),
                    Math.Clamp(
                        source.Json is null
                            ? source.Int(source.Field('\t', 1))
                            : source.Number("rotation", 0),
                        0,
                        3
                    ),
                    1
                );
                box.Picks(source.ItemIds(legacyField: 3, legacySeparator: '\t', legacyList: '\r'));
                break;
            case "wf_act_move_furni_to":
            {
                box.Action(
                    source.Json is null
                        ? source.Int(source.Field('\t', 2))
                        : source.Number("delay", 0)
                );

                var direction = source.Json is null
                    ? source.Int(source.Field('\t', 0))
                    : source.Number("direction", 0);

                box.Ints(
                    direction is 0 or 2 or 4 or 6 ? direction : 0,
                    Math.Clamp(
                        source.Json is null
                            ? source.Int(source.Field('\t', 1))
                            : source.Number("spacing", 1),
                        1,
                        5
                    )
                );
                box.Picks(source.ItemIds(legacyField: 3, legacySeparator: '\t', legacyList: '\r'));
                break;
            }
            case "wf_act_move_to_dir":
            {
                box.Action(
                    source.Json is null
                        ? source.Int(source.Field('\t', 0))
                        : source.Number("delay", 0)
                );

                var start = source.Json is { } json
                    ? Direction(json["start_direction"])
                    : source.Int(source.Field('\t', 1));
                var blocked = source.Json is null
                    ? source.Int(source.Field('\t', 2))
                    : source.Number("blocked_action", 0);

                box.Ints(Math.Clamp(start, 0, 7), Math.Clamp(blocked, 0, 6), 1);
                box.Picks(
                    source.Json?["items"] is JsonArray items
                        ? items.Select(x => x?["item_id"]?.GetValue<int>() ?? 0)
                        : source.Raw.Split('\t').Skip(4).Select(x => source.Int(x.Split(':')[0]))
                );
                break;
            }
            case "wf_act_match_to_sshot":
            {
                var (state, direction, position, delay, picks) = Snapshot(
                    source,
                    "items",
                    isAction: true
                );

                box.Action(delay);
                box.Ints(state ? 1 : 0, direction ? 1 : 0, position ? 1 : 0, 0);
                box.Picks(picks.Select(x => x.ItemId));
                box.Snapshot.AddRange(picks);
                break;
            }
            case "wf_act_give_reward":
                lost = GiveReward(source, box, classnameOf);
                break;
            case "wf_act_bot_teleport" or "wf_act_bot_move":
                box.Action(source.Delay('\t', 0));

                if (source.Json is { } botJson)
                {
                    box.Text = botJson["bot_name"]?.GetValue<string>() ?? string.Empty;
                    box.Picks(Ints(botJson["items"]));
                }
                else
                {
                    var parts = (source.Field('\t', 1) ?? string.Empty).Split(';');

                    box.Text = parts[0];
                    box.Picks(parts.Skip(1).Select(source.Int));
                }

                break;
            case "wf_act_bot_talk" or "wf_act_bot_talk_to_avatar":
            {
                box.Action(source.Delay('\t', 0));

                var mode = source.Json is null
                    ? source.Int(source.Field('\t', 1))
                    : source.Number("mode", 0);
                var bot = source.Json?["bot_name"]?.GetValue<string>()
                    ?? source.Field('\t', 2)
                    ?? string.Empty;
                var message = source.Json?["message"]?.GetValue<string>()
                    ?? source.Field('\t', 3)
                    ?? string.Empty;

                box.Ints(mode == 1 ? 1 : 0, -1, 0);
                box.Text = $"{bot}\t{message}";
                break;
            }
            case "wf_act_bot_follow_avatar":
                box.Action(source.Delay('\t', 0));
                box.Ints(
                    (
                        source.Json is null
                            ? source.Int(source.Field('\t', 1))
                            : source.Number("mode", 1)
                    ) == 1
                        ? 1
                        : 0
                );
                box.Text = source.Json?["bot_name"]?.GetValue<string>()
                    ?? source.Field('\t', 2)
                    ?? string.Empty;
                break;
            case "wf_act_bot_give_handitem":
            {
                box.Action(source.Delay('\t', 0));

                var handItem = source.Json is null
                    ? source.Int(source.Field('\t', 1))
                    : source.Number("item_id", 2);

                // Turbo refuses the whole box for a hand item out of range.
                if (handItem is < 0 or > 10_000)
                    return null;

                box.Ints(handItem);
                box.Text = source.Json?["bot_name"]?.GetValue<string>()
                    ?? source.Field('\t', 2)
                    ?? string.Empty;
                break;
            }
            case "wf_act_bot_clothes":
            {
                box.Action(source.Delay('\t', 0));

                var bot = source.Json?["bot_name"]?.GetValue<string>()
                    ?? source.Field('\t', 1)
                    ?? string.Empty;
                var look = source.Json?["look"]?.GetValue<string>()
                    ?? source.Field('\t', 2)
                    ?? string.Empty;

                box.Text = $"{bot}\t{look}";
                break;
            }

            // Conditions
            case "wf_cnd_has_furni_on"
            or "wf_cnd_not_furni_on":
            {
                box.Condition();

                var all = source.Json is { } json
                    ? json["all"]?.GetValue<bool>() ?? false
                    : source.Field(':', 0) == "1";

                box.Ints(all ? 1 : 0);
                box.Picks(source.ItemIds(legacyField: 1, legacySeparator: ':'));
                break;
            }
            case "wf_cnd_furnis_hv_avtrs" or "wf_cnd_not_hv_avtrs":
                box.Condition();
                box.Ints(interaction == "wf_cnd_furnis_hv_avtrs" ? 1 : 0);
                box.Picks(source.ItemIds(legacyField: 1, legacySeparator: ':'));
                break;
            case "wf_cnd_stuff_is"
            or "wf_cnd_not_stuff_is"
            or "wf_cnd_trggrer_on_frn"
            or "wf_cnd_not_trggrer_on":
                box.Condition();
                box.Picks(source.ItemIds(legacyField: 0, legacySeparator: '\0'));
                break;
            case "wf_cnd_user_count_in" or "wf_cnd_not_user_count":
                box.Condition();
                box.Ints(
                    Math.Clamp(
                        source.Json is null
                            ? source.Int(source.Field(':', 0))
                            : source.Number("lowerLimit", 1),
                        0,
                        125
                    ),
                    Math.Clamp(
                        source.Json is null
                            ? source.Int(source.Field(':', 1))
                            : source.Number("upperLimit", 50),
                        0,
                        125
                    )
                );
                break;
            case "wf_cnd_wearing_effect" or "wf_cnd_not_wearing_fx":
                box.Condition();
                box.Ints(
                    Math.Max(
                        0,
                        source.Json is null ? source.Int(source.Raw) : source.Number("effectId", 0)
                    )
                );
                break;
            case "wf_cnd_wearing_badge" or "wf_cnd_not_wearing_b":
                box.Condition();
                box.Text = source.Json?["badge"]?.GetValue<string>() ?? source.Raw;
                break;
            case "wf_cnd_has_handitem":
            {
                box.Condition();

                var handItem = source.Json is null
                    ? source.Int(source.Raw)
                    : source.Number("handItemId", 2);

                if (handItem is < 0 or > 10_000)
                    return null;

                box.Ints(handItem);
                break;
            }
            case "wf_cnd_time_more_than":
                box.Condition();
                box.Ints(
                    Math.Max(
                        1,
                        source.Json is null ? source.Int(source.Raw) : source.Number("cycles", 20)
                    )
                );
                break;
            case "wf_cnd_time_less_than":
                box.Condition();
                box.Ints(
                    Math.Clamp(
                        source.Json is null ? source.Int(source.Raw) : source.Number("cycles", 20),
                        1,
                        1201
                    )
                );
                break;
            case "wf_cnd_match_snapshot" or "wf_cnd_not_match_snap":
            {
                var (state, direction, position, _, picks) = Snapshot(
                    source,
                    "settings",
                    isAction: false
                );

                box.Condition();
                box.Ints(state ? 1 : 0, direction ? 1 : 0, position ? 1 : 0, 0);
                box.Picks(picks.Select(x => x.ItemId));
                box.Snapshot.AddRange(picks);
                break;
            }
            case "wf_cnd_actor_in_team" or "wf_cnd_not_in_team":
                box.Condition();
                box.Ints(
                    Math.Clamp(
                        source.Json is { } teamJson
                            ? Team(teamJson["teamColor"])
                            : source.Int(source.Raw),
                        1,
                        4
                    )
                );
                break;
            case "wf_cnd_date_rng_active":
                box.Condition();
                box.Ints(
                    source.Json is null
                        ? source.Int(source.Field('\t', 0))
                        : source.Number("startDate", 0),
                    source.Json is null
                        ? source.Int(source.Field('\t', 1))
                        : source.Number("endDate", 0)
                );
                break;
            case "wf_cnd_actor_in_group" or "wf_cnd_not_in_group":
                box.Condition();
                break;

            // Add-ons
            case "wf_xtra_random":
                box.Ints(0, 1);
                break;
            case "wf_xtra_unseen":
                break;
            case "wf_xtra_or_eval":
                box.Ints(1, 0, 0);
                break;

            default:
                return null;
        }

        return new WiredResult(box.ToJson(), box.Snapshot, lost);
    }

    /// <summary>
    /// A give-reward box: Turbo gives badges and furni (by classname); Morningstar's credits,
    /// points, respect and catalog rewards have no place there and are said to be lost.
    /// </summary>
    private static string? GiveReward(
        MorningstarWired source,
        Box box,
        Func<int, string?> classnameOf
    )
    {
        if (source.Json is not { } json)
            return "a give-reward box in Morningstar's old format";

        box.Action(source.Number("delay", 0));
        box.Ints(
            Math.Clamp(source.Number("reward_time", 0), 0, 3),
            json["unique_rewards"]?.GetValue<bool>() == true ? 1 : 0,
            Math.Clamp(source.Number("limit", 0), 0, 1000),
            Math.Clamp(source.Number("limit_interval", 1), 1, 9999)
        );

        var rewards = new List<string>();
        var dropped = 0;

        foreach (var reward in json["rewards"] as JsonArray ?? [])
        {
            var data = reward?["data"]?.GetValue<string>() ?? string.Empty;
            var probability = Math.Clamp(reward?["probability"]?.GetValue<int>() ?? 100, 0, 100);

            if (reward?["badge"]?.GetValue<bool>() == true && data.Length > 0)
                rewards.Add($"0,{data},{probability}");
            else if (
                data.StartsWith("furni#", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(data.AsSpan(6), out var baseId)
                && classnameOf(baseId) is { } classname
            )
                rewards.Add($"1,{classname},{probability}");
            else
                dropped++;
        }

        box.Text = string.Join(';', rewards.Take(20));

        return dropped > 0
            ? "a reward Turbo's box cannot give (credits, points, respect or a catalog offer)"
            : null;
    }

    private static (
        bool State,
        bool Direction,
        bool Position,
        int Delay,
        List<SnapshotPick> Picks
    ) Snapshot(MorningstarWired source, string listKey, bool isAction)
    {
        var picks = new List<SnapshotPick>();

        if (source.Json is { } json)
        {
            foreach (var entry in json[listKey] as JsonArray ?? [])
                if (entry?["item_id"]?.GetValue<int>() is int id and > 0)
                    picks.Add(
                        new SnapshotPick(
                            id,
                            entry["state"]?.GetValue<string>() ?? "0",
                            entry["rotation"]?.GetValue<int>() ?? 0,
                            entry["x"]?.GetValue<int>() ?? 0,
                            entry["y"]?.GetValue<int>() ?? 0
                        )
                    );

            return (
                json["state"]?.GetValue<bool>() ?? false,
                json["direction"]?.GetValue<bool>() ?? false,
                json["position"]?.GetValue<bool>() ?? false,
                source.Number("delay", 0),
                picks
            );
        }

        // count:id-state-rot-x-y;...:S:D:P[:delay]
        var parts = source.Raw.Split(':');

        if (parts.Length < 5)
            return (false, false, false, 0, picks);

        foreach (var item in parts[1].Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var fields = item.Split('-');

            if (fields.Length >= 5 && int.TryParse(fields[0], out var id))
                picks.Add(
                    new SnapshotPick(
                        id,
                        fields[1],
                        source.Int(fields[2]),
                        source.Int(fields[3]),
                        source.Int(fields[4])
                    )
                );
        }

        return (
            parts[2] == "1",
            parts[3] == "1",
            parts[4] == "1",
            isAction && parts.Length > 5 ? source.Int(parts[5]) : 0,
            picks
        );
    }

    private static int Team(JsonNode? node) =>
        node?.GetValueKind() == JsonValueKind.Number
            ? node.GetValue<int>()
            : Math.Max(0, Array.IndexOf(TEAMS, node?.GetValue<string>()?.ToUpperInvariant()));

    private static int Direction(JsonNode? node) =>
        node?.GetValueKind() == JsonValueKind.Number
            ? node.GetValue<int>()
            : Math.Max(0, Array.IndexOf(DIRECTIONS, node?.GetValue<string>()?.ToUpperInvariant()));

    private static IEnumerable<int> Ints(JsonNode? node) =>
        (node as JsonArray ?? []).Select(x =>
            x?.GetValueKind() == JsonValueKind.Number ? x.GetValue<int>() : 0
        );

    /// <summary>A Morningstar box's stored settings: JSON when it starts with <c>{</c>, else the old form.</summary>
    private sealed class MorningstarWired
    {
        public required string Raw { get; init; }

        public JsonObject? Json { get; init; }

        public static MorningstarWired Parse(string? data)
        {
            var raw = data?.Trim() ?? string.Empty;

            if (raw.StartsWith('{'))
            {
                try
                {
                    return new MorningstarWired
                    {
                        Raw = raw,
                        Json = JsonNode.Parse(raw) as JsonObject,
                    };
                }
                catch (JsonException)
                {
                    // Not JSON after all: read it as the old form.
                }
            }

            return new MorningstarWired { Raw = raw };
        }

        public string? Field(char separator, int index)
        {
            var parts = Raw.Split(separator);

            return index < parts.Length ? parts[index] : null;
        }

        public int Int(string? value) =>
            int.TryParse(
                value?.Trim(),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var parsed
            )
                ? parsed
                : 0;

        public int Number(string key, int fallback)
        {
            var node = Json?[key];

            if (node is null)
                return Json is null ? Int(Raw) : fallback;

            return node.GetValueKind() switch
            {
                JsonValueKind.Number => node.GetValue<double>() switch
                {
                    > int.MaxValue => int.MaxValue,
                    < int.MinValue => int.MinValue,
                    var d => (int)d,
                },
                JsonValueKind.True => 1,
                JsonValueKind.False => 0,
                JsonValueKind.String => Int(node.GetValue<string>()),
                _ => fallback,
            };
        }

        /// <summary>An action's delay: JSON <c>delay</c>, or the old form's field.</summary>
        public int Delay(char separator, int index) =>
            Json is null ? Int(Field(separator, index)) : Number("delay", 0);

        /// <summary>A message box's text: JSON <c>message</c>, or the old form after the delay.</summary>
        public string Message(int index)
        {
            if (Json is not null)
                return Json["message"]?.GetValue<string>() ?? string.Empty;

            var split = Raw.IndexOf('\t', StringComparison.Ordinal);

            return index > 0 && split >= 0 ? Raw[(split + 1)..] : string.Empty;
        }

        public int Team(string key, char separator, int index) =>
            Json is null ? Int(Field(separator, index)) : WiredSettings.Team(Json[key]);

        /// <summary>
        /// The picked furni: JSON <c>itemIds</c>, or the old form's list (in field
        /// <paramref name="legacyField"/> of <paramref name="legacySeparator"/>, ids separated
        /// by <paramref name="legacyList"/>).
        /// </summary>
        public IEnumerable<int> ItemIds(
            int legacyField,
            char legacySeparator,
            char legacyList = ';'
        )
        {
            if (Json is not null)
                return WiredSettings.Ints(Json["itemIds"]);

            var field = legacySeparator == '\0' ? Raw : Field(legacySeparator, legacyField);

            return (field ?? string.Empty)
                .Split(
                    legacyList,
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
                )
                .Select(Int);
        }
    }

    /// <summary>The Turbo box being written.</summary>
    private sealed class Box
    {
        private readonly List<int> _ints = [];
        private readonly List<int> _picks = [];
        private JsonArray _definitionSpecifics = [];

        public string Text { get; set; } = string.Empty;

        public List<SnapshotPick> Snapshot { get; } = [];

        public void Ints(params int[] values) => _ints.AddRange(values);

        public void Picks(IEnumerable<int> ids)
        {
            foreach (var id in ids)
                if (id > 0 && !_picks.Contains(id) && _picks.Count < MAX_PICKS)
                    _picks.Add(id);
        }

        /// <summary>An action: its delay in pulses, within Turbo's cap.</summary>
        public void Action(int delay) =>
            _definitionSpecifics = [Math.Clamp(delay, 0, MAX_DELAY_PULSES)];

        /// <summary>A condition: quantifier 0, all.</summary>
        public void Condition() => _definitionSpecifics = [0];

        public JsonObject ToJson() =>
            new()
            {
                ["IntParams"] = new JsonArray([.. _ints.Select(x => (JsonNode)x)]),
                ["StringParam"] = Text,
                ["StuffIds"] = new JsonArray([.. _picks.Select(x => (JsonNode)x)]),
                ["StuffIds2"] = new JsonArray(),
                ["VariableIds"] = new JsonArray(),
                ["DefinitionSpecifics"] = _definitionSpecifics,
                ["TypeSpecifics"] = new JsonArray(),
            };
    }
}
