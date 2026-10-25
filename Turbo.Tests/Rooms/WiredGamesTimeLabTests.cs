using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Messages.Outgoing.Room.Action;
using Turbo.Primitives.Messages.Outgoing.Room.Session;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Conditions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Counters;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;
using Turbo.Rooms.Wired;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// The games, teams and time boxes against the official server, one test per measured case
/// (official client capture 2026-10-24, WIN63-202610201032): the counter's lifecycle, Clock
/// Reaches Time, Time Matches and Date Range bounds, scoring only in a game, how tied teams place,
/// the team conditions with no user, and what an out-of-range save stores. Case ids are
/// category.code.case.
/// </summary>
public sealed class WiredGamesTimeLabTests
{
    private const int CLOCK = 30;
    private const int PAD = 21;
    private const int CLICK_ME = 23;
    private const int TRIGGER = 1;
    private const int EFFECT = 3;
    private const int BOX = 2;
    private const int PLAYER = 5;

    private readonly WiredRoom _room;
    private long _now = 10_000;

    public WiredGamesTimeLabTests() => _room = new WiredRoom(8, 8);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private RoomWiredSystem Wired => _room.Harness.Module<RoomWiredSystem>();

    private RoomGameSystem Game => _room.Harness.Module<RoomGameSystem>();

    /// <summary>
    /// action.28.R7b: Adjust Clock set a never-started counter to 30 and Control Clock reset left
    /// it at 30 (4/4); a counter started and stopped first, set to 30 and reset, showed 0 (1/1).
    /// </summary>
    [Fact]
    public async Task Reset_does_nothing_on_a_counter_never_started_action_28_R7b()
    {
        var clock = AddClock();

        await clock.AdjustAsync(WiredOperatorType.Set, 60, Ct);
        await clock.ControlAsync(WiredClockControlType.Reset, Ct);
        clock.GetState().Should().Be(30, "never started, the reset does nothing");

        await clock.ControlAsync(WiredClockControlType.Start, Ct);
        await clock.ControlAsync(WiredClockControlType.Stop, Ct);
        await clock.AdjustAsync(WiredOperatorType.Set, 60, Ct);
        await clock.ControlAsync(WiredClockControlType.Reset, Ct);
        clock.GetState().Should().Be(0, "started and stopped first, the reset goes to 0");
    }

    /// <summary>
    /// action.28.R8: a never-started counter adjusted to 30 counts on from 30 when started (1/1);
    /// a counter stopped before starts again from 0 (13, 26 and 33 went to 0, 3/3).
    /// </summary>
    [Fact]
    public async Task Start_counts_on_from_an_adjusted_time_and_afresh_after_a_stop_action_28_R8()
    {
        var clock = AddClock();

        await clock.AdjustAsync(WiredOperatorType.Set, 60, Ct);
        await clock.ControlAsync(WiredClockControlType.Start, Ct);
        await RunClockUntilAsync(clock, 31);

        await clock.ControlAsync(WiredClockControlType.Stop, Ct);
        clock.GetState().Should().Be(31);
        await clock.ControlAsync(WiredClockControlType.Start, Ct);
        clock.GetState().Should().Be(0, "a stopped counter starts afresh");
        await RunClockUntilAsync(clock, 2);

        await clock.ControlAsync(WiredClockControlType.Pause, Ct);
        await clock.ControlAsync(WiredClockControlType.Resume, Ct);
        clock.GetState().Should().Be(2, "a paused counter runs on");
    }

    /// <summary>
    /// trigger.15.R5: Clock Reaches Time 0:30 did not fire when Adjust Clock set the stopped
    /// counter to exactly 0:30 (4/4) nor when it added 5 s to a running 0:29 (2/2); it fired on
    /// the counted 29 -&gt; 30 (1/1).
    /// </summary>
    [Fact]
    public async Task Clock_reaches_time_fires_only_on_the_counted_tick_trigger_15_R5()
    {
        var clock = AddClock();
        var pad = _room.AddFloorItem(PAD, 3, 5);
        _room.AddBox<WiredTriggerCounterReachesTime>(TRIGGER, 0, 0, "wf_trg_clock_counter");
        _room.AddBox<WiredActionToggleItemState>(EFFECT, 0, 0, "wf_act_toggle_state");
        (
            await _room.SaveAsync<UpdateTriggerMessage>(
                TRIGGER,
                intParams: [30, 0, 0],
                stuffIds: [CLOCK],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ]
            )
        ).Should().BeTrue();
        await SaveToggleAsync();
        await StackChangedAsync();

        await clock.AdjustAsync(WiredOperatorType.Set, 60, Ct);
        (await RunAsync(pad, 1_000)).Should().Be(0, "set to 0:30 while stopped");

        await clock.AdjustAsync(WiredOperatorType.Set, 56, Ct);
        await clock.ControlAsync(WiredClockControlType.Start, Ct);
        (await RunAsync(pad, 2_000)).Should().Be(1, "counted 29 -> 30");

        await clock.AdjustAsync(WiredOperatorType.Set, 58, Ct);
        await clock.AdjustAsync(WiredOperatorType.Add, 10, Ct);
        (await RunAsync(pad, 1_500)).Should().Be(0, "jumped 29 -> 34");
    }

    /// <summary>
    /// condition.28.B2: hours 22..2 wrap round midnight (held at 23 in Hawaii 2/2, failed at 10 in
    /// London 5/5); both bounds count and min == max is that hour (8..10, 10..11, 10..10 at 10;
    /// 22..23, 23..23, 22..0 at 23: 29/29).
    /// </summary>
    [Theory]
    [InlineData(23, 22, 2, true)]
    [InlineData(10, 22, 2, false)]
    [InlineData(1, 22, 2, true)]
    [InlineData(3, 22, 2, false)]
    [InlineData(10, 8, 10, true)]
    [InlineData(10, 10, 11, true)]
    [InlineData(10, 10, 10, true)]
    [InlineData(23, 22, 0, true)]
    [InlineData(23, 23, 23, true)]
    [InlineData(12, 2, 22, true)]
    public async Task Time_matches_wraps_a_min_above_the_max_condition_28_B2(
        int hour,
        int min,
        int max,
        bool holds
    )
    {
        // London in January is UTC.
        _room.Clock.Set(new DateTime(2026, 1, 15, hour, 30, 0, DateTimeKind.Utc));
        var condition = _room.AddBox<WiredConditionTimeMatches>(BOX, 0, 0, "wf_cnd_match_time");

        (
            await _room.SaveAsync<UpdateConditionMessage>(
                BOX,
                intParams: [0, 0, 1, 0, 0, 0, 0, min, max],
                stringParam: "Europe/London",
                definitionSpecifics: [0]
            )
        )
            .Should()
            .BeTrue();

        condition.Evaluate(Context()).Should().Be(holds, $"{min}..{max} at {hour}h");
    }

    /// <summary>
    /// condition.24.B2: past start and future end held (1/1); with an end of 0 (the editor sends
    /// one int when the end is empty, stored [start, 0]), [0, 0] or an end before the start it
    /// never held (9/9); a start past 2038, negative in the editor, saved as sent (1/1).
    /// </summary>
    [Theory]
    [InlineData(-3600, 3600, true)]
    [InlineData(-3600, null, false)]
    [InlineData(3600, null, false)]
    [InlineData(-3600, 0, false)]
    [InlineData(0, 0, false)]
    [InlineData(-3600, -7200, false)]
    public async Task Date_range_needs_an_end_after_the_start_condition_24_B2(
        int startFromNow,
        int? endFromNow,
        bool holds
    )
    {
        var now = (int)_room.Clock.GetUtcNow().ToUnixTimeSeconds();
        int[] ints = endFromNow switch
        {
            null => [now + startFromNow],
            0 when startFromNow == 0 => [0, 0],
            0 => [now + startFromNow, 0],
            _ => [now + startFromNow, now + endFromNow.Value],
        };
        var condition = _room.AddBox<WiredConditionDateRangeActive>(
            BOX,
            0,
            0,
            "wf_cnd_date_rng_active"
        );

        (await SaveConditionAsync(ints)).Should().BeTrue();

        condition.Evaluate(Context()).Should().Be(holds);
    }

    [Fact]
    public async Task A_negative_date_range_start_is_saved_as_sent_condition_24_B2()
    {
        var condition = _room.AddBox<WiredConditionDateRangeActive>(
            BOX,
            0,
            0,
            "wf_cnd_date_rng_active"
        );

        (await SaveConditionAsync([-2147480000, 0])).Should().BeTrue();

        condition.GetSnapshot().IntParams.Should().Equal(-2147480000, 0);
        condition.Evaluate(Context()).Should().BeFalse();
    }

    /// <summary>
    /// action.6.B1: Give Score fired by a user in no team did nothing (2/2), and by a user in red
    /// with no game started added nothing (1/1); once the Wired Game Counter started (Game Starts
    /// fired) it scored.
    /// </summary>
    [Fact]
    public async Task Give_score_counts_only_while_a_game_runs_action_6_B1()
    {
        var counter = await BuildGiveScoreAsync(points: 5, timesPerGame: 0);

        await ClickAsync();
        Game.GetScore(GameTeamType.Red).Should().Be(0, "in no team");

        (await Game.JoinTeamAsync((PlayerId)(100 + PLAYER), GameTeamType.Red, Ct))
            .Should()
            .BeTrue();
        await ClickAsync();
        Game.GetScore(GameTeamType.Red).Should().Be(0, "no game running");

        await counter.ControlAsync(WiredClockControlType.Start, Ct);
        Game.IsGameRunning.Should().BeTrue("the game counter started the game");
        await ClickAsync();
        Game.GetScore(GameTeamType.Red).Should().Be(5);
    }

    /// <summary>
    /// action.6.R1: in a running game, Give Score 5 with "times per game" 2 fired three times
    /// added 10 (the third run nothing, 1/1).
    /// </summary>
    [Fact]
    public async Task Times_per_game_caps_the_box_in_a_game_action_6_R1()
    {
        var counter = await BuildGiveScoreAsync(points: 5, timesPerGame: 2);
        (await Game.JoinTeamAsync((PlayerId)(100 + PLAYER), GameTeamType.Red, Ct))
            .Should()
            .BeTrue();
        await counter.ControlAsync(WiredClockControlType.Start, Ct);

        for (var i = 0; i < 3; i++)
            await ClickAsync();

        Game.GetScore(GameTeamType.Red).Should().Be(10);
    }

    /// <summary>
    /// action.14.R2 / action.6.R3 (wlab4): Give Score To Team [2,0,1] on a periodic stack added 2
    /// each firing while the game ran (0 -> 6 after 3), nothing after Game Ends (8 stays, 2/2); the
    /// score was kept after the end and back to 0 at the next start (official client capture
    /// 2026-10-24, 1/1 each).
    /// </summary>
    [Fact]
    public async Task Action_14_R2_team_score_counts_while_the_game_runs_and_is_kept_until_the_next_start()
    {
        await Game.StartGameAsync(Ct);

        for (var i = 0; i < 4; i++)
            (await Game.GiveScoreAsync(GameTeamType.Red, 2, default, default, 0, Ct))
                .Should()
                .BeTrue();

        await Game.EndGameAsync(Ct);

        for (var i = 0; i < 2; i++)
            (await Game.GiveScoreAsync(GameTeamType.Red, 2, default, default, 0, Ct))
                .Should()
                .BeFalse("the game ended");

        Game.GetScore(GameTeamType.Red).Should().Be(8, "kept after the end");

        await Game.StartGameAsync(Ct);

        Game.GetScore(GameTeamType.Red).Should().Be(0, "the next start resets it");
    }

    /// <summary>
    /// condition.31.R5 / R1: red reached 30, then green reached 30: red 1st held, green 1st and red
    /// 2nd failed, an empty blue at 0 was not 1st; green at 31 was 1st and red 2nd (8/8). With
    /// nobody scored, red is 1st (2/2).
    /// </summary>
    [Fact]
    public async Task A_tie_goes_to_the_team_that_reached_the_score_first_condition_31_R5()
    {
        await Game.StartGameAsync(Ct);
        Game.GetPlacement(GameTeamType.Red).Should().Be(1, "nobody scored");

        await Game.GiveScoreAsync(GameTeamType.Red, 30, default, default, 0, Ct);
        await Game.GiveScoreAsync(GameTeamType.Green, 30, default, default, 0, Ct);

        Game.GetPlacement(GameTeamType.Red).Should().Be(1, "red reached 30 first");
        Game.GetPlacement(GameTeamType.Green).Should().Be(2);
        Game.GetPlacement(GameTeamType.Blue).Should().Be(3);

        await Game.GiveScoreAsync(GameTeamType.Green, 1, default, default, 0, Ct);

        Game.GetPlacement(GameTeamType.Green).Should().Be(1);
        Game.GetPlacement(GameTeamType.Red).Should().Be(2);
    }

    /// <summary>
    /// condition.31.R2 / condition.34.R2 / condition.6.R2: under Periodically (no user), the
    /// triggerer's team and Actor In Team pass with quantifier All (vacuous) and fail with Any;
    /// an explicit team is no user's, so red 1st / red = 0 pass under both (6/6 each).
    /// </summary>
    [Theory]
    [InlineData("condition.31.R2", "wf_cnd_team_has_rank", new[] { 0, 0 }, 0, true)]
    [InlineData("condition.31.R2", "wf_cnd_team_has_rank", new[] { 0, 0 }, 1, false)]
    [InlineData("condition.31.R2", "wf_cnd_team_has_rank", new[] { 1, 0 }, 0, true)]
    [InlineData("condition.31.R2", "wf_cnd_team_has_rank", new[] { 1, 0 }, 1, true)]
    [InlineData("condition.34.R2", "wf_cnd_team_has_score", new[] { 0, 0, 1 }, 0, true)]
    [InlineData("condition.34.R2", "wf_cnd_team_has_score", new[] { 0, 0, 1 }, 1, false)]
    [InlineData("condition.34.R2", "wf_cnd_team_has_score", new[] { 1, 0, 1 }, 0, true)]
    [InlineData("condition.34.R2", "wf_cnd_team_has_score", new[] { 1, 0, 1 }, 1, true)]
    [InlineData("condition.6.R2", "wf_cnd_actor_in_team", new[] { 0 }, 0, true)]
    [InlineData("condition.6.R2", "wf_cnd_actor_in_team", new[] { 0 }, 1, false)]
    [InlineData("condition.6.R2", "wf_cnd_actor_in_team", new[] { 1 }, 0, true)]
    [InlineData("condition.6.R2", "wf_cnd_actor_in_team", new[] { 1 }, 1, false)]
    public async Task Team_conditions_with_no_user_pass_for_all_and_fail_for_any(
        string caseId,
        string name,
        int[] ints,
        int quantifier,
        bool passes
    )
    {
        var condition = AddTeamCondition(name);

        (await SaveConditionAsync(ints, quantifier)).Should().BeTrue(caseId);

        condition.Evaluate(Context()).Should().Be(passes, caseId);
        condition.GetQuantifierType().Should().Be((byte)WiredQuantifierType.Users);
    }

    /// <summary>
    /// condition.31.R1 / condition.34.R1: fired by a user in no team, the triggerer's team is 1st
    /// failed (3/3) and has = 0, &lt; 1 and &gt; 0 points all failed (3/3).
    /// </summary>
    [Theory]
    [InlineData("condition.31.R1", "wf_cnd_team_has_rank", new[] { 0, 0 })]
    [InlineData("condition.34.R1", "wf_cnd_team_has_score", new[] { 0, 0, 1 })]
    [InlineData("condition.34.R1", "wf_cnd_team_has_score", new[] { 0, 1, 0 })]
    [InlineData("condition.34.R1", "wf_cnd_team_has_score", new[] { 0, 0, 2 })]
    public async Task The_triggerers_team_fails_for_a_user_in_no_team(
        string caseId,
        string name,
        int[] ints
    )
    {
        _room.Enter(PLAYER, 1, 1);
        var condition = AddTeamCondition(name);

        (await SaveConditionAsync(ints)).Should().BeTrue(caseId);

        var ctx = Context();
        ctx.Selected.SelectedAvatarIds.Add(PLAYER);
        condition.Evaluate(ctx).Should().BeFalse(caseId);
    }

    /// <summary>
    /// action.6.E11, trigger.19.E10, trigger.12.E10, condition.3.E10: saved by packet past what
    /// the editor offers, each answered WiredSaveSuccess and stored as the Open showed: Give
    /// Score's points and times and the short repeater's units pulled into range, the long
    /// repeater's units and Time Elapsed's time kept as sent.
    /// </summary>
    [Theory]
    [InlineData("action.6.E11", "wf_act_give_score", new[] { 0, 0 }, new[] { 0, 0 })]
    [InlineData("action.6.E11", "wf_act_give_score", new[] { 1001, 0 }, new[] { 1000, 0 })]
    [InlineData("action.6.E11", "wf_act_give_score", new[] { -1001, 0 }, new[] { -1000, 0 })]
    [InlineData("action.6.E11", "wf_act_give_score", new[] { 5, 11 }, new[] { 5, 10 })]
    [InlineData("trigger.19.E10", "wf_trg_period_short", new[] { 11 }, new[] { 10 })]
    [InlineData("trigger.12.E10", "wf_trg_period_long", new[] { 121 }, new[] { 121 })]
    [InlineData("condition.3.E10", "wf_cnd_time_more_than", new[] { 1 }, new[] { 1 })]
    [InlineData("condition.3.E10", "wf_cnd_time_more_than", new[] { 1202 }, new[] { 1202 })]
    public async Task An_out_of_range_save_is_stored_as_measured(
        string caseId,
        string name,
        int[] sent,
        int[] stored
    )
    {
        FurnitureWiredLogic box = name switch
        {
            "wf_act_give_score" => _room.AddBox<WiredActionGiveScore>(BOX, 0, 0, name),
            "wf_trg_period_short" => _room.AddBox<WiredTriggerShortPeriodically>(BOX, 0, 0, name),
            "wf_trg_period_long" => _room.AddBox<WiredTriggerLongPeriodically>(BOX, 0, 0, name),
            _ => _room.AddBox<WiredConditionTimerMoreThan>(BOX, 0, 0, name),
        };

        var saved = box switch
        {
            FurnitureWiredTriggerLogic => await _room.SaveAsync<UpdateTriggerMessage>(
                BOX,
                intParams: sent
            ),
            FurnitureWiredConditionLogic => await SaveConditionAsync(sent),
            _ => await _room.SaveAsync<UpdateActionMessage>(
                BOX,
                intParams: sent,
                definitionSpecifics: [0]
            ),
        };

        saved.Should().BeTrue(caseId);
        box.GetSnapshot().IntParams.Should().Equal(stored, caseId);
    }

    /// <summary>
    /// action.10.B1: Leave Team on a user in no team still sent that user YouArePlayingGame false,
    /// and no AvatarEffect (13-05-42, 13-06-29, 2/2).
    /// </summary>
    [Fact]
    public async Task Leave_team_tells_a_user_in_no_team_they_are_not_playing_action_10_B1()
    {
        _room.Enter(PLAYER, 1, 1);

        (await Game.LeaveTeamAsync((PlayerId)(100 + PLAYER), Ct)).Should().BeFalse();

        _room
            .Harness.Fakes.SentToPlayer<YouArePlayingGameMessageComposer>(100 + PLAYER)
            .Should()
            .ContainSingle(x => !x.IsPlaying);
        _room.Harness.Fakes.Sent<AvatarEffectMessageComposer>().Should().BeEmpty();
    }

    /// <summary>
    /// condition.4.R2 / condition.3.R2: Time Elapsed Less [20] and More [20] under a 500 ms
    /// repeater, Reset Timers at t: Less passed on 21 pulses (t+0.27 to t+10.27 s) and failed
    /// after, More failed on 20 and passed from t+10.27 s, so that one pulse passed both
    /// (23-01-35, 52 pulses).
    /// </summary>
    [Fact]
    public async Task Time_elapsed_less_and_more_both_hold_on_the_pulse_at_the_time_condition_4_R2()
    {
        // A fresh box holds 20 (condition.3.E6 / condition.4.E6).
        var less = _room.AddBox<WiredConditionTimerLessThan>(BOX, 0, 0, "wf_cnd_time_less_than");
        var more = _room.AddBox<WiredConditionTimerMoreThan>(
            BOX + 1,
            1,
            0,
            "wf_cnd_time_more_than"
        );

        Wired.ResetTimers(_room.Harness.Room.NowMs());
        _room.Clock.AdvanceMs(270);

        var passedLess = new List<int>();
        var passedMore = new List<int>();

        for (var pulse = 0; pulse < 26; pulse++)
        {
            if (less.Evaluate(Context()))
                passedLess.Add(pulse);

            if (more.Evaluate(Context()))
                passedMore.Add(pulse);

            _room.Clock.AdvanceMs(500);
        }

        passedLess.Should().Equal(Enumerable.Range(0, 21));
        passedMore.Should().Equal(Enumerable.Range(20, 6));
    }

    /// <summary>
    /// action.28.R2: Control Clock start fired again by its 3 s repeater on the running counter
    /// at 3 sent "0" in that firing, and the counter counted 1, 2, 3 from there (11-08-31).
    /// </summary>
    [Fact]
    public async Task Start_on_a_running_counter_restarts_it_at_0_action_28_R2()
    {
        var clock = AddClock();

        await clock.ControlAsync(WiredClockControlType.Start, Ct);
        await RunClockUntilAsync(clock, 3);

        await clock.ControlAsync(WiredClockControlType.Start, Ct);
        clock.GetState().Should().Be(0, "a start restarts a running counter");
        await RunClockUntilAsync(clock, 2);
    }

    /// <summary>
    /// action.9.E11: Join Team saved with team 0 or 5 was refused and kept its [2, 0] (2/2). The
    /// official server answered with its generic ErrorReport (code 1), ours with the generic
    /// wired save error: either way nothing is stored.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    public async Task Join_team_refuses_a_team_out_of_range_action_9_E11(int team)
    {
        var box = _room.AddBox<WiredActionJoinTeam>(BOX, 0, 0, "wf_act_join_team");
        (
            await _room.SaveAsync<UpdateActionMessage>(
                BOX,
                intParams: [2, 0],
                definitionSpecifics: [0]
            )
        )
            .Should()
            .BeTrue();

        (
            await _room.SaveAsync<UpdateActionMessage>(
                BOX,
                intParams: [team, 0],
                definitionSpecifics: [0]
            )
        )
            .Should()
            .BeFalse();

        box.GetSnapshot().IntParams.Should().Equal(2, 0);
    }

    private FurnitureCounterClockLogic AddClock() =>
        (FurnitureCounterClockLogic)
            _room
                .AddFloorItem(
                    CLOCK,
                    3,
                    3,
                    "wf_upcounter",
                    createLogic: (factory, ctx) => new FurnitureCounterClockLogic(factory, ctx)
                )
                .Logic;

    private FurnitureWiredConditionLogic AddTeamCondition(string name) =>
        name switch
        {
            "wf_cnd_team_has_rank" => _room.AddBox<WiredConditionTeamHasRank>(BOX, 0, 0, name),
            "wf_cnd_team_has_score" => _room.AddBox<WiredConditionTeamHasScore>(BOX, 0, 0, name),
            _ => _room.AddBox<WiredConditionHabboInTeam>(BOX, 0, 0, name),
        };

    private Task<bool> SaveConditionAsync(int[] ints, int quantifier = 0) =>
        _room.SaveAsync<UpdateConditionMessage>(
            BOX,
            intParams: ints,
            definitionSpecifics: [quantifier]
        );

    // A click on CLICK_ME gives the clicking user's team points; a game counter to start the game.
    private async Task<FurnitureCounterClockLogic> BuildGiveScoreAsync(int points, int timesPerGame)
    {
        _room.Enter(PLAYER, 1, 1);
        _room.AddFloorItem(CLICK_ME, 7, 4);
        var counter = (FurnitureCounterClockLogic)
            _room
                .AddFloorItem(
                    CLOCK,
                    3,
                    3,
                    "wf_game_upcounter1",
                    createLogic: (factory, ctx) => new FurnitureGameCounterLogic(factory, ctx)
                )
                .Logic;
        _room.AddBox<WiredTriggerClickFurni>(TRIGGER, 0, 0, "wf_trg_click_furni");
        _room.AddBox<WiredActionGiveScore>(BOX, 0, 0, "wf_act_give_score");

        (
            await _room.SaveAsync<UpdateTriggerMessage>(
                TRIGGER,
                stuffIds: [CLICK_ME],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ]
            )
        ).Should().BeTrue();
        (
            await _room.SaveAsync<UpdateActionMessage>(
                BOX,
                intParams: [points, timesPerGame],
                playerSources:
                [
                    [WiredPlayerSourceType.TriggeredUser],
                ],
                definitionSpecifics: [0]
            )
        ).Should().BeTrue();
        await StackChangedAsync();
        await TickAsync(1);

        return counter;
    }

    private async Task ClickAsync()
    {
        await _room
            .FloorItem(CLICK_ME)
            .Logic.OnClickAsync(
                ActionContext.CreateForPlayer((PlayerId)(100 + PLAYER), (RoomId)1),
                0,
                Ct
            );
        await TickAsync(2);
    }

    private async Task TickAsync(int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            _now += 1_000;
            await Wired.ProcessWiredAsync(_now, dormant: false, Ct);
        }
    }

    // EFFECT toggles PAD.
    private async Task SaveToggleAsync() => (
            await _room.SaveAsync<UpdateActionMessage>(
                EFFECT,
                intParams: [0],
                stuffIds: [PAD],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ],
                definitionSpecifics: [0]
            )
        ).Should().BeTrue();

    private Task StackChangedAsync() =>
        Wired.OnRoomEventAsync(
            new RoomWiredStackChangedEvent
            {
                RoomId = 1,
                CausedBy = ActionContext.CreateForSystem(1),
                StackIds = [_room.Map.ToIdx(0, 0)],
            },
            Ct
        );

    private WiredProcessingContext Context() =>
        new(_room.Harness.Room)
        {
            Event = new RoomWiredStackChangedEvent
            {
                RoomId = 1,
                CausedBy = ActionContext.CreateForSystem(1),
                StackIds = [],
            },
            Stack = _room.Harness.Fakes.Create<IWiredStack>(),
        };

    // The room's timers and wired run every 50 ms for `forMs`; each PAD toggle is a firing.
    private async Task<int> RunAsync(IRoomFloorItem pad, long forMs)
    {
        var timers = _room.Harness.Module<RoomTimerSystem>();
        var fired = 0;

        for (var at = 0; at < forMs; at += 50)
        {
            var before = pad.Logic.GetState();
            _room.Clock.AdvanceMs(50);
            await timers.ProcessTimersAsync(_room.Harness.Room.NowMs(), Ct);
            await Wired.ProcessWiredAsync(_room.Harness.Room.NowMs(), dormant: false, Ct);

            if (pad.Logic.GetState() != before)
                fired++;
        }

        return fired;
    }

    private async Task RunClockUntilAsync(FurnitureCounterClockLogic clock, int seconds)
    {
        var timers = _room.Harness.Module<RoomTimerSystem>();

        for (var i = 0; i < 2_000 && clock.GetState() < seconds; i++)
        {
            _room.Clock.AdvanceMs(50);
            await timers.ProcessTimersAsync(_room.Harness.Room.NowMs(), Ct);
        }

        clock.GetState().Should().Be(seconds);
    }
}
