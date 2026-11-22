using System.Text.Json.Nodes;
using Turbo.Import.Morningstar;
using Turbo.Import.Morningstar.Formats;
using Turbo.Primitives.Furniture.Enums;
using Xunit;

namespace Turbo.Tests.Import;

/// <summary>
/// Morningstar's stored formats, read the way Morningstar writes them and written the way Turbo's
/// logics read them: the wired settings of its older delimited form, and the furni whose state is
/// a format of its own.
/// </summary>
public class MorningstarFormatsTests
{
    private static readonly DateTime NOW = new(2026, 11, 22, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>The limits the host gives an import by default.</summary>
    private static readonly ImportLimits LIMITS = MorningstarImportPlayersTests.Options().Limits;

    private static int[] Ints(JsonObject wired) =>
        [.. wired["IntParams"]!.AsArray().Select(x => x!.GetValue<int>())];

    private static int[] Picks(JsonObject wired) =>
        [.. wired["StuffIds"]!.AsArray().Select(x => x!.GetValue<int>())];

    private static int Delay(JsonObject wired) => wired["DefinitionSpecifics"]![0]!.GetValue<int>();

    private static WiredResult Wired(string interaction, string data) =>
        WiredSettings.Translate(interaction, data, id => id == 9 ? "throne" : null, LIMITS)!;

    [Fact]
    public void Old_form_toggle_keeps_its_delay_and_picks()
    {
        var wired = Wired("wf_act_toggle_state", "6\t11;12;13").Wired;

        Assert.Equal(6, Delay(wired));
        Assert.Equal([11, 12, 13], Picks(wired));
    }

    [Fact]
    public void Old_form_move_rotate_reads_its_carriage_return_list()
    {
        var wired = Wired("wf_act_move_rotate", "4\t1\t3\t21\r22").Wired;

        Assert.Equal([4, 1, 1], Ints(wired));
        Assert.Equal(3, Delay(wired));
        Assert.Equal([21, 22], Picks(wired));
    }

    [Fact]
    public void Old_form_walk_on_reads_the_third_field()
    {
        Assert.Equal([5, 6], Picks(Wired("wf_trg_walks_on_furni", "0:x:5;6").Wired));
    }

    [Fact]
    public void Periods_are_turned_from_milliseconds_into_turbos_units()
    {
        // 2.5 s is 5 pulses; a long period of 30 s is 6 of Turbo's 5 s units.
        Assert.Equal([5], Ints(Wired("wf_trg_periodically", """{"repeatTime":2500}""").Wired));
        Assert.Equal([6], Ints(Wired("wf_trg_period_long", "30000").Wired));
    }

    [Fact]
    public void A_team_is_read_by_name_or_by_number()
    {
        Assert.Equal(
            [3, 0],
            Ints(Wired("wf_act_join_team", """{"team":"BLUE","delay":0}""").Wired)
        );
        Assert.Equal([4, 0], Ints(Wired("wf_act_join_team", "0\t4").Wired));
        Assert.Equal([2], Ints(Wired("wf_cnd_actor_in_team", """{"teamColor":"GREEN"}""").Wired));
    }

    [Fact]
    public void Ints_are_kept_inside_turbos_rules()
    {
        // Out of range would reset the whole box: a delay past 60 s, a score past 1000.
        var wired = Wired("wf_act_give_score", "5000;99;400").Wired;

        Assert.Equal([1000, 10], Ints(wired));
        Assert.Equal(120, Delay(wired));
    }

    [Fact]
    public void A_bot_box_joins_its_name_and_message_as_turbo_reads_them()
    {
        var wired = Wired(
            "wf_act_bot_talk",
            """{"bot_name":"Frank","mode":1,"message":"Hello","delay":2}"""
        ).Wired;

        Assert.Equal("Frank\tHello", wired["StringParam"]!.GetValue<string>());
        Assert.Equal([1, -1, 0], Ints(wired));
    }

    [Fact]
    public void A_hand_item_turbo_would_refuse_drops_the_settings()
    {
        Assert.Null(WiredSettings.Translate("wf_cnd_has_handitem", "20000", _ => null, LIMITS));
    }

    [Fact]
    public void A_reward_box_keeps_badges_and_furni_and_says_what_it_lost()
    {
        var result = Wired(
            "wf_act_give_reward",
            """{"limit":5,"given":0,"reward_time":2,"unique_rewards":true,"limit_interval":6,"rewards":[{"id":1,"badge":true,"data":"WIN","probability":50},{"id":1,"badge":false,"data":"furni#9","probability":50},{"id":1,"badge":false,"data":"credits#10","probability":10}],"delay":0}"""
        );

        Assert.Equal([2, 1, 5, 6], Ints(result.Wired));
        Assert.Equal("0,WIN,50;1,throne,50", result.Wired["StringParam"]!.GetValue<string>());
        Assert.NotNull(result.Lost);
    }

    [Fact]
    public void An_old_form_snapshot_keeps_what_it_remembers()
    {
        var result = Wired("wf_cnd_match_snapshot", "2:31-1-2-4-5;32-0-6-1-1:1:0:1");

        Assert.Equal([1, 0, 1, 0], Ints(result.Wired));
        Assert.Equal(
            [new SnapshotPick(31, "1", 2, 4, 5), new SnapshotPick(32, "0", 6, 1, 1)],
            result.Snapshot
        );
    }

    private static JsonNode? Stuff(
        string logic,
        string extra,
        FurnitureCategory category = FurnitureCategory.Default
    ) =>
        FurnitureState
            .Translate(
                new DefinitionRef(1, "x", logic, ProductType.Floor, category, "default"),
                new ItemState
                {
                    ExtraData = extra,
                    Now = NOW,
                    Limits = LIMITS,
                }
            )
            ?["stuff"]?["Data"];

    [Fact]
    public void A_badge_display_turns_owner_date_badge_into_turbos_order()
    {
        Assert.Equal(
            ["0", "ADM", "Alice", "05-03-2024"],
            Stuff("badge_display", "Alice\t5-3-2024\tADM")!
                .AsArray()
                .Select(x => x!.GetValue<string>())
        );
    }

    [Fact]
    public void A_mannequin_keeps_only_clothing_and_its_gender()
    {
        var map = Stuff("mannequin", "f:hr-100-61.hd-180-1.ch-210-66.lg-270-82:Party look")!;

        Assert.Equal("F", map["GENDER"]!.GetValue<string>());
        Assert.Equal("ch-210-66.lg-270-82", map["FIGURE"]!.GetValue<string>());
        Assert.Equal("Party look", map["OUTFIT_NAME"]!.GetValue<string>());
    }

    [Fact]
    public void A_toner_becomes_numbers_and_a_love_lock_strings()
    {
        Assert.Equal(
            [1, 120, 200, 90],
            Stuff("background_toner", "1:120:200:90")!.AsArray().Select(x => x!.GetValue<int>())
        );
        Assert.Equal(
            ["1", "Alice", "Bob", "hr-1", "hr-2", "14-02-2025"],
            Stuff("love_lock", "1\tAlice\tBob\thr-1\thr-2\t14-2-2025")!
                .AsArray()
                .Select(x => x!.GetValue<string>())
        );
    }

    [Fact]
    public void Room_ads_keep_a_link_with_a_query_string()
    {
        var map = Stuff(
            "room_branding",
            "imageUrl=http://a.test/ad.png;clickUrl=http://a.test/?ref=hotel;offsetX=3;"
        )!;

        Assert.Equal("http://a.test/?ref=hotel", map["clickUrl"]!.GetValue<string>());
        Assert.Equal("3", map["offsetX"]!.GetValue<string>());
    }

    [Fact]
    public void A_plain_state_that_is_no_number_is_zero_but_a_poster_keeps_its_value()
    {
        Assert.Equal("0", Stuff("default_floor", "abc")!.GetValue<string>());
        Assert.Equal(
            "1.1",
            Stuff("default_wall", "1.1", FurnitureCategory.Poster)!.GetValue<string>()
        );
    }

    [Fact]
    public void A_heightmap_turbo_cannot_read_is_refused()
    {
        Assert.Equal("x00\r\nx1a", Heightmap.Clean("X00\rx1A\r"));
        Assert.Null(Heightmap.Clean("x0?"));
        Assert.Null(Heightmap.Clean("xxx\rxxx"));
    }
}
