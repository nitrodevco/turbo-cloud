using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Conditions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// Condition Evaluation (<c>wf_xtra_or_eval</c>): its furni slot holds the conditions the mode
/// applies to ("If the selection is empty, it will apply to all available conditions."); only
/// conditions and add-ons can be picked, a furni source outside [0,100,200,201] is refused, the
/// mode is clamped into range and the counted mode stored as sent (official client capture
/// 2026-10-22T22-56-43-click / 22-57-02-UpdateAddon). The stack: a click, three user-count
/// conditions (one holds, two do not), joining the red team.
/// </summary>
public sealed class WiredConditionEvaluationTests
{
    private const int CLICK_ME = 23;
    private const int LAMP = 24;
    private const int TRIGGER = 1;
    private const int HOLDS = 2;
    private const int FAILS = 3;
    private const int FAILS_TOO = 4;
    private const int EVAL = 5;
    private const int JOIN = 6;

    private readonly WiredRoom _room = new(8, 8);
    private long _now = 10_000;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private RoomWiredSystem Wired => _room.Harness.Module<RoomWiredSystem>();

    private RoomGameSystem Game => _room.Harness.Module<RoomGameSystem>();

    private WiredAddonConditionsEval Eval => (WiredAddonConditionsEval)_room.FloorItem(EVAL).Logic;

    public WiredConditionEvaluationTests()
    {
        _room.Enter(5, 1, 1);
        _room.AddFloorItem(CLICK_ME, 7, 4);
        _room.AddFloorItem(LAMP, 7, 5);
        _room.AddBox<WiredTriggerClickFurni>(TRIGGER, 0, 0, "wf_trg_click_furni");
        _room.AddBox<WiredConditionRoomHabboCount>(HOLDS, 0, 0, "wf_cnd_user_count_in");
        _room.AddBox<WiredConditionRoomHabboCount>(FAILS, 0, 0, "wf_cnd_user_count_in");
        _room.AddBox<WiredConditionRoomHabboCount>(FAILS_TOO, 0, 0, "wf_cnd_user_count_in");
        _room.AddBox<WiredAddonConditionsEval>(EVAL, 0, 0, "wf_xtra_or_eval");
        _room.AddBox<WiredActionJoinTeam>(JOIN, 0, 0, "wf_act_join_team");
    }

    [Fact]
    public async Task At_least_one_of_the_picked_conditions_has_to_hold()
    {
        await BuildAsync(picked: [FAILS, FAILS_TOO]);

        await ClickAsync();

        Game.GetTeam((PlayerId)105).Should().Be(GameTeamType.None);
    }

    [Fact]
    public async Task The_conditions_not_picked_must_each_hold()
    {
        await BuildAsync(picked: [HOLDS, FAILS]);

        await ClickAsync();

        Game.GetTeam((PlayerId)105).Should().Be(GameTeamType.None);
    }

    [Fact]
    public async Task With_nothing_picked_the_mode_applies_to_every_condition()
    {
        await BuildAsync(picked: []);

        await ClickAsync();

        Game.GetTeam((PlayerId)105).Should().Be(GameTeamType.Red);
    }

    [Fact]
    public async Task Only_conditions_and_add_ons_can_be_picked()
    {
        (await SaveEvalAsync([1, 0, 0], stuffIds: [HOLDS, JOIN]))
            .ErrorKey.Should()
            .Be("wiredfurni.error.condition_evaluation_furni");
        (await SaveEvalAsync([1, 0, 0], stuffIds: [LAMP])).IsSaved.Should().BeFalse();
        (await SaveEvalAsync([1, 0, 0], stuffIds: [HOLDS, EVAL])).IsSaved.Should().BeTrue();
    }

    [Fact]
    public async Task A_furni_source_the_slot_does_not_offer_is_refused()
    {
        var result = await SaveEvalAsync(
            [1, 0, 0],
            furniSources:
            [
                [WiredFurniSourceType.AllRoomItems],
            ]
        );

        result.IsSaved.Should().BeFalse();
        result.ErrorKey.Should().Be("wiredfurni.error.invalid_furni_source");
    }

    [Theory]
    [InlineData(new[] { 9, 0, 0 }, new[] { 3, 0, 0 })]
    [InlineData(new[] { -1, 5, 3 }, new[] { -1, 5, 3 })]
    public async Task The_mode_is_clamped_and_the_counted_mode_stored_as_sent(
        int[] sent,
        int[] stored
    )
    {
        (await SaveEvalAsync(sent)).IsSaved.Should().BeTrue();

        Eval.GetSnapshot().IntParams.Should().Equal(stored);
    }

    // One of the three conditions holds, so the count is 1 (official client capture
    // WIN63-202610201032, 2026-10-23T00-05-33 / 00-06-44 / 00-07-24-UpdateAddon, A true and B false).
    [Theory]
    [InlineData(0, 1, false)]
    [InlineData(1, 1, true)]
    [InlineData(2, -1, true)]
    [InlineData(3, 0, false)]
    [InlineData(3, 1, true)]
    [InlineData(3, 1001, true)]
    [InlineData(4, 1, false)]
    [InlineData(4, 2, true)]
    [InlineData(5, 1, true)]
    [InlineData(5, 2, false)]
    [InlineData(6, 0, false)]
    [InlineData(6, 1, false)]
    public async Task A_counted_mode_compares_the_conditions_that_hold_with_the_number(
        int countedMode,
        int number,
        bool passes
    )
    {
        await BuildAsync(picked: [], evalParams: [-1, countedMode, number]);

        await ClickAsync();

        Game.GetTeam((PlayerId)105).Should().Be(passes ? GameTeamType.Red : GameTeamType.None);
    }

    [Theory]
    [InlineData(new[] { -1, -3, 5 }, new[] { -1, 0, 5 })]
    [InlineData(new[] { -1, 0, 99999 }, new[] { -1, 0, 99999 })]
    [InlineData(new[] { -1, 0, -1000 }, new[] { -1, 0, -1000 })]
    [InlineData(new[] { -1, 6, 1001 }, new[] { -1, 6, 1001 })]
    public async Task A_negative_counted_mode_is_stored_as_0_and_the_number_as_sent(
        int[] sent,
        int[] stored
    )
    {
        (await SaveEvalAsync(sent)).IsSaved.Should().BeTrue();

        Eval.GetSnapshot().IntParams.Should().Equal(stored);
    }

    private async Task BuildAsync(int[] picked, int[]? evalParams = null)
    {
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

        // One user in the room: 1-50 holds, 5-50 does not.
        foreach (var (id, min) in new[] { (HOLDS, 1), (FAILS, 5), (FAILS_TOO, 5) })
            (await _room.SaveAsync<UpdateConditionMessage>(id, intParams: [min, 50]))
                .Should()
                .BeTrue();

        (await SaveEvalAsync(evalParams ?? [1, 0, 0], stuffIds: picked)).IsSaved.Should().BeTrue();
        (
            await _room.SaveAsync<UpdateActionMessage>(
                JOIN,
                intParams: [(int)GameTeamType.Red, (int)WiredTeamType.Wired],
                playerSources:
                [
                    [WiredPlayerSourceType.TriggeredUser],
                ],
                definitionSpecifics: [0]
            )
        ).Should().BeTrue();

        await Wired.OnRoomEventAsync(
            new RoomWiredStackChangedEvent
            {
                RoomId = 1,
                CausedBy = ActionContext.CreateForSystem(1),
                StackIds = [_room.Map.ToIdx(0, 0)],
            },
            Ct
        );

        await TickAsync(1);
    }

    private Task<Turbo.Primitives.Rooms.Wired.WiredSaveResult> SaveEvalAsync(
        int[] intParams,
        int[]? stuffIds = null,
        WiredFurniSourceType[][]? furniSources = null
    ) =>
        _room.SaveWithResultAsync<UpdateAddonMessage>(
            EVAL,
            intParams: intParams,
            stuffIds: stuffIds,
            furniSources: furniSources
        );

    private async Task ClickAsync()
    {
        await _room
            .FloorItem(CLICK_ME)
            .Logic.OnClickAsync(ActionContext.CreateForPlayer((PlayerId)105, (RoomId)1), 0, Ct);
        await TickAsync(4);
    }

    private async Task TickAsync(int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            _now += 1_000;
            await Wired.ProcessWiredAsync(_now, dormant: false, Ct);
        }
    }
}
