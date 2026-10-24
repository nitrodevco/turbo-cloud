using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Conditions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;
using Turbo.Rooms.Wired;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// The games, teams and time boxes as Habbo's editors save them: every value each editor can send
/// (the sliders, radios and checkboxes of the WIN63-202610201032 editors) is kept, and the timers
/// count in Habbo's units. What a never-saved box opens with is in
/// <see cref="WiredOfficialFreshDefaultsTests"/>. Case ids are category.code.case, e.g.
/// action.6.E9: the box by its wired code, then the case (E editor).
/// </summary>
public sealed class WiredGamesTimeEditorTests
{
    private const int BOX = 2;

    private readonly WiredRoom _room = new(8, 8);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static TheoryData<string, Type, string, int, int[]> EditorValues =>
        new()
        {
            // case, box, class, param index, every boundary or choice the official editor can save
            // (the other params keep the fresh box's defaults)
            {
                "action.6.E9",
                typeof(WiredActionGiveScore),
                "wf_act_give_score",
                0,
                [-1000, -1, 1, 1000]
            },
            { "action.6.E10", typeof(WiredActionGiveScore), "wf_act_give_score", 1, [0, 1, 10] },
            {
                "action.14.E9",
                typeof(WiredActionGiveScoreToTeam),
                "wf_act_give_score_tm",
                0,
                [-1000, -1, 1, 1000]
            },
            {
                "action.14.E10",
                typeof(WiredActionGiveScoreToTeam),
                "wf_act_give_score_tm",
                1,
                [0, 1, 10]
            },
            {
                "action.14.E11",
                typeof(WiredActionGiveScoreToTeam),
                "wf_act_give_score_tm",
                2,
                [1, 2, 3, 4]
            },
            { "action.9.E9", typeof(WiredActionJoinTeam), "wf_act_join_team", 0, [1, 2, 3, 4] },
            { "action.9.E10", typeof(WiredActionJoinTeam), "wf_act_join_team", 1, [0, 1, 2] },
            {
                "action.28.E9",
                typeof(WiredActionControlClock),
                "wf_act_control_clock",
                0,
                [0, 1, 2, 3, 4]
            },
            { "action.38.E11", typeof(WiredActionAdjustClock), "wf_act_adjust_clock", 2, [0, 1] },
            {
                "action.38.E12",
                typeof(WiredActionAdjustClock),
                "wf_act_adjust_clock",
                3,
                [0, 1, 2]
            },
            {
                "condition.24.E9",
                typeof(WiredConditionDateRangeActive),
                "wf_cnd_date_rng_active",
                0,
                [0, 1_798_761_600, int.MaxValue]
            },
            {
                "condition.24.E10",
                typeof(WiredConditionDateRangeActive),
                "wf_cnd_date_rng_active",
                1,
                [0, 1_798_761_600, int.MaxValue]
            },
            {
                "condition.28.E9",
                typeof(WiredConditionTimeMatches),
                "wf_cnd_match_time",
                0,
                [0, 1]
            },
            {
                "condition.28.E10",
                typeof(WiredConditionTimeMatches),
                "wf_cnd_match_time",
                1,
                [0, 1]
            },
            {
                "condition.28.E11",
                typeof(WiredConditionTimeMatches),
                "wf_cnd_match_time",
                2,
                [0, 1]
            },
            {
                "condition.28.E12",
                typeof(WiredConditionTimeMatches),
                "wf_cnd_match_time",
                3,
                [0, 59]
            },
            {
                "condition.28.E13",
                typeof(WiredConditionTimeMatches),
                "wf_cnd_match_time",
                4,
                [0, 59]
            },
            {
                "condition.28.E14",
                typeof(WiredConditionTimeMatches),
                "wf_cnd_match_time",
                5,
                [0, 59]
            },
            {
                "condition.28.E15",
                typeof(WiredConditionTimeMatches),
                "wf_cnd_match_time",
                6,
                [0, 59]
            },
            {
                "condition.28.E16",
                typeof(WiredConditionTimeMatches),
                "wf_cnd_match_time",
                7,
                [0, 23]
            },
            {
                "condition.28.E17",
                typeof(WiredConditionTimeMatches),
                "wf_cnd_match_time",
                8,
                [0, 23]
            },
            {
                "condition.29.E9",
                typeof(WiredConditionDateMatches),
                "wf_cnd_match_date",
                0,
                [0, 1]
            },
            {
                "condition.29.E10",
                typeof(WiredConditionDateMatches),
                "wf_cnd_match_date",
                1,
                [0, 1]
            },
            {
                "condition.29.E11",
                typeof(WiredConditionDateMatches),
                "wf_cnd_match_date",
                2,
                [0, 1, 64, 127]
            },
            {
                "condition.29.E12",
                typeof(WiredConditionDateMatches),
                "wf_cnd_match_date",
                3,
                [1, 31]
            },
            {
                "condition.29.E13",
                typeof(WiredConditionDateMatches),
                "wf_cnd_match_date",
                4,
                [1, 31]
            },
            {
                "condition.29.E14",
                typeof(WiredConditionDateMatches),
                "wf_cnd_match_date",
                5,
                [0, 1, 2048, 4095]
            },
            {
                "condition.29.E15",
                typeof(WiredConditionDateMatches),
                "wf_cnd_match_date",
                6,
                [0, 9999]
            },
            {
                "condition.29.E16",
                typeof(WiredConditionDateMatches),
                "wf_cnd_match_date",
                7,
                [0, 9999]
            },
            {
                "condition.3.E9",
                typeof(WiredConditionTimerMoreThan),
                "wf_cnd_time_more_than",
                0,
                [2, 1201]
            },
            {
                "condition.4.E9",
                typeof(WiredConditionTimerLessThan),
                "wf_cnd_time_less_than",
                0,
                [2, 1201]
            },
            {
                "condition.31.E9",
                typeof(WiredConditionTeamHasRank),
                "wf_cnd_team_has_rank",
                0,
                [0, 1, 2, 3, 4]
            },
            {
                "condition.31.E10",
                typeof(WiredConditionTeamHasRank),
                "wf_cnd_team_has_rank",
                1,
                [0, 1, 2, 3]
            },
            {
                "condition.34.E9",
                typeof(WiredConditionTeamHasScore),
                "wf_cnd_team_has_score",
                0,
                [0, 1, 2, 3, 4]
            },
            {
                "condition.34.E11",
                typeof(WiredConditionTeamHasScore),
                "wf_cnd_team_has_score",
                2,
                [0, 1, 2]
            },
            {
                "condition.35.E11",
                typeof(WiredConditionCounterTimeMatch),
                "wf_cnd_counter_time_matches",
                2,
                [0, 1]
            },
            {
                "condition.35.E12",
                typeof(WiredConditionCounterTimeMatch),
                "wf_cnd_counter_time_matches",
                3,
                [0, 1, 2]
            },
            {
                "condition.6.E9",
                typeof(WiredConditionHabboInTeam),
                "wf_cnd_actor_in_team",
                0,
                [0, 1, 2, 3, 4]
            },
            {
                "trigger.10.E9",
                typeof(WiredTriggerScoreAchieved),
                "wf_trg_score_achieved",
                0,
                [1, 1000]
            },
            {
                "trigger.10.E10",
                typeof(WiredTriggerScoreAchieved),
                "wf_trg_score_achieved",
                1,
                [0, 1, 2, 3, 4]
            },
            {
                "trigger.12.E9",
                typeof(WiredTriggerLongPeriodically),
                "wf_trg_period_long",
                0,
                [1, 120]
            },
            {
                "trigger.15.E9",
                typeof(WiredTriggerCounterReachesTime),
                "wf_trg_clock_counter",
                0,
                [0, 59]
            },
            {
                "trigger.15.E11",
                typeof(WiredTriggerCounterReachesTime),
                "wf_trg_clock_counter",
                2,
                [0, 1]
            },
            {
                "trigger.19.E9",
                typeof(WiredTriggerShortPeriodically),
                "wf_trg_period_short",
                0,
                [1, 10]
            },
            { "trigger.3.E9", typeof(WiredTriggerAtTime), "wf_trg_at_given_time", 0, [1, 1200] },
            {
                "trigger.6.E9",
                typeof(WiredTriggerPeriodically),
                "wf_trg_periodically",
                0,
                [1, 120]
            },
        };

    [Theory]
    [MemberData(nameof(EditorValues))]
    public async Task Every_value_the_official_editor_saves_is_kept(
        string caseId,
        Type logic,
        string name,
        int index,
        int[] values
    )
    {
        var box = Add(logic, name);
        await box.LoadWiredAsync(Ct);
        var defaults = box.GetSnapshot().DefaultIntParams.ToArray();

        foreach (var value in values)
        {
            var ints = defaults.ToArray();
            ints[index] = value;

            (await SaveAsync(box, ints)).Should().BeTrue($"{caseId} saves {value}");
            box.GetSnapshot().IntParams.Should().Equal(ints, caseId);
        }
    }

    /// <summary>
    /// trigger.6/12/19.E9, trigger.3.E9: Periodically counts half-second pulses, the long one five
    /// seconds a unit, the short one 50 ms a unit; "at given time" two pulses a second.
    /// </summary>
    [Theory]
    [InlineData("trigger.6.E9", typeof(WiredTriggerPeriodically), "wf_trg_periodically", 1, 500)]
    [InlineData(
        "trigger.6.E9",
        typeof(WiredTriggerPeriodically),
        "wf_trg_periodically",
        120,
        60_000
    )]
    [InlineData(
        "trigger.12.E9",
        typeof(WiredTriggerLongPeriodically),
        "wf_trg_period_long",
        1,
        5_000
    )]
    [InlineData(
        "trigger.12.E9",
        typeof(WiredTriggerLongPeriodically),
        "wf_trg_period_long",
        120,
        600_000
    )]
    [InlineData(
        "trigger.19.E9",
        typeof(WiredTriggerShortPeriodically),
        "wf_trg_period_short",
        1,
        50
    )]
    [InlineData(
        "trigger.19.E9",
        typeof(WiredTriggerShortPeriodically),
        "wf_trg_period_short",
        10,
        500
    )]
    public async Task A_periodic_timer_counts_in_its_official_unit(
        string caseId,
        Type logic,
        string name,
        int units,
        int periodMs
    )
    {
        var box = (WiredTriggerPeriodically)Add(logic, name);

        (await SaveAsync(box, [units])).Should().BeTrue(caseId);
        // The saved stack is rebuilt before the timers next run, which loads the box again.
        await box.LoadWiredAsync(Ct);

        box.GetPeriodicDelayMs().Should().Be(periodMs, caseId);
    }

    [Theory]
    [InlineData(1, 500)]
    [InlineData(1200, 600_000)]
    public async Task At_given_time_counts_half_second_pulses_trigger_3_E9(int pulses, long atMs)
    {
        var box = _room.AddBox<WiredTriggerAtTime>(BOX, 0, 0, "wf_trg_at_given_time");

        (await SaveAsync(box, [pulses])).Should().BeTrue();

        box.GetTargetMs().Should().Be(atMs);
    }

    /// <summary>
    /// trigger.15.E9/E11: the clock trigger's seconds (0 to 59), minutes and half second add up
    /// to the half seconds the counter is compared in.
    /// </summary>
    [Fact]
    public async Task The_clock_trigger_reaches_its_time_in_half_seconds_trigger_15_E9_E11()
    {
        var box = _room.AddBox<WiredTriggerCounterReachesTime>(BOX, 0, 0, "wf_trg_clock_counter");

        (await SaveAsync(box, [59, 99, 1])).Should().BeTrue();

        box.GetTargetHalfSeconds().Should().Be(((99 * 60) + 59) * 2 + 1);
    }

    /// <summary>
    /// The time zones Habbo offers the time and date conditions (<c>wired.timezones</c> in the
    /// official external variables, 2026-10-12): each one is read in that zone, not in the room's
    /// fallback. condition.28.E18 / condition.29.E17.
    /// </summary>
    public static TheoryData<string> OfficialTimezones =>
        new(
            "Europe/London",
            "America/Antigua",
            "America/Barbados",
            "America/Guyana",
            "America/Jamaica",
            "America/Puerto_Rico",
            "Australia/Adelaide",
            "Australia/Brisbane",
            "Australia/Darwin",
            "Australia/Eucla",
            "Australia/Lord_Howe",
            "Australia/Perth",
            "Australia/Sydney",
            "Canada/Atlantic",
            "Canada/Central",
            "Canada/Eastern",
            "Canada/Mountain",
            "Canada/Newfoundland",
            "Canada/Pacific",
            "Canada/Saskatchewan",
            "Canada/Yukon",
            "Pacific/Auckland",
            "US/Alaska",
            "US/Aleutian",
            "US/Arizona",
            "US/Central",
            "US/East-Indiana",
            "US/Eastern",
            "US/Hawaii",
            "US/Indiana-Starke",
            "US/Michigan",
            "US/Mountain",
            "US/Pacific",
            "US/Samoa"
        );

    [Theory]
    [MemberData(nameof(OfficialTimezones))]
    public async Task Time_matches_reads_the_hour_in_each_official_timezone_condition_28_E18(
        string timezone
    )
    {
        var utc = _room.Clock.GetUtcNow();
        var hour = TimeZoneInfo
            .ConvertTime(utc, TimeZoneInfo.FindSystemTimeZoneById(timezone))
            .Hour;
        var condition = _room.AddBox<WiredConditionTimeMatches>(BOX, 0, 0, "wf_cnd_match_time");

        (await SaveAsync(condition, [0, 0, 1, 0, 0, 0, 0, hour, hour], timezone)).Should().BeTrue();
        condition.Evaluate(Context()).Should().BeTrue($"{timezone} reads {hour}h");

        var other = (hour + 12) % 24;
        (await SaveAsync(condition, [0, 0, 1, 0, 0, 0, 0, other, other], timezone))
            .Should()
            .BeTrue();
        condition.Evaluate(Context()).Should().BeFalse($"{timezone} does not read {other}h");
    }

    private FurnitureWiredLogic Add(Type logic, string name) =>
        (FurnitureWiredLogic)
            typeof(WiredRoom)
                .GetMethod(nameof(WiredRoom.AddBox))!
                .MakeGenericMethod(logic)
                .Invoke(_room, [BOX, 0, 0, name])!;

    private Task<bool> SaveAsync(FurnitureWiredLogic box, int[] ints, string text = "") =>
        box switch
        {
            FurnitureWiredTriggerLogic => _room.SaveAsync<UpdateTriggerMessage>(
                BOX,
                intParams: ints,
                stringParam: text
            ),
            FurnitureWiredConditionLogic => _room.SaveAsync<UpdateConditionMessage>(
                BOX,
                intParams: ints,
                stringParam: text,
                definitionSpecifics: [0]
            ),
            _ => _room.SaveAsync<UpdateActionMessage>(
                BOX,
                intParams: ints,
                stringParam: text,
                definitionSpecifics: [0]
            ),
        };

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
}
