using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Conditions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// "Execute Wired Stacks" runs the stacks it picks "regardless of existing conditions or
/// triggers" (official furnidata 2026-10-13, id 4834 wf_act_call_stacks): a called stack whose own
/// condition fails still runs its effects. The negative call does the same (official client
/// capture, WIN63-202610171745: the callee's effect ran with its condition false 5/5, true 2/2).
/// </summary>
public sealed class WiredCallStacksConditionsTests
{
    private const int CLICK_ME = 30;
    private const int OTHER = 31;

    // Caller at (0,0), callee at (2,0).
    private const int CALLER_TRIGGER = 1;
    private const int CALLER_ACTION = 2;
    private const int CALLEE_TRIGGER = 3;
    private const int CALLEE_CONDITION = 4;
    private const int CALLEE_ACTION = 5;
    private const int CALLER_CONDITION = 6;

    private readonly WiredRoom _room = new(8, 8);
    private long _now = 10_000;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private RoomWiredSystem Wired => _room.Harness.Module<RoomWiredSystem>();

    public WiredCallStacksConditionsTests()
    {
        _room.Enter(5, 1, 1);
        _room.AddFloorItem(CLICK_ME, 7, 7);
        _room.AddFloorItem(OTHER, 6, 7);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_called_stack_runs_its_effects_whatever_its_conditions_say(
        bool conditionPasses
    )
    {
        await BuildAsync(conditionPasses);

        await ClickAsync();
        await TickAsync(2);

        Logged("callee").Should().Be(1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_negative_call_runs_the_called_stack_whatever_its_conditions_say(
        bool conditionPasses
    )
    {
        await BuildAsync(conditionPasses, negative: true, callerPasses: false);

        await ClickAsync();
        await TickAsync(2);

        Logged("callee").Should().Be(1);
    }

    [Fact]
    public async Task A_negative_call_does_not_run_when_the_callers_conditions_pass()
    {
        await BuildAsync(conditionPasses: false, negative: true, callerPasses: true);

        await ClickAsync();
        await TickAsync(2);

        Logged("callee").Should().Be(0);
    }

    private async Task BuildAsync(
        bool conditionPasses,
        bool negative = false,
        bool callerPasses = true
    )
    {
        // The callee listens to another furni, which nobody clicks.
        _room.AddBox<WiredTriggerClickFurni>(CALLEE_TRIGGER, 2, 0, "wf_trg_click_furni");
        (
            await _room.SaveAsync<UpdateTriggerMessage>(
                CALLEE_TRIGGER,
                stuffIds: [OTHER],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ]
            )
        ).Should().BeTrue();

        // Its selection holds no furni: "exactly 0" passes, "exactly 5" fails.
        _room.AddBox<WiredConditionSelectQuantity>(CALLEE_CONDITION, 2, 0, "wf_cnd_slc_quantity");
        (
            await _room.SaveAsync<UpdateConditionMessage>(
                CALLEE_CONDITION,
                intParams: [0, conditionPasses ? 0 : 5, 1],
                furniSources:
                [
                    [WiredFurniSourceType.SelectorItems],
                ],
                playerSources:
                [
                    [WiredPlayerSourceType.SelectorUsers],
                ]
            )
        ).Should().BeTrue();

        _room.AddBox<WiredActionWriteToLogs>(CALLEE_ACTION, 2, 0, "wf_act_log");
        (
            await _room.SaveAsync<UpdateActionMessage>(
                CALLEE_ACTION,
                intParams: [(int)WiredLogLevelType.Warning],
                stringParam: "callee",
                definitionSpecifics: [0]
            )
        )
            .Should()
            .BeTrue();

        _room.AddBox<WiredTriggerClickFurni>(CALLER_TRIGGER, 0, 0, "wf_trg_click_furni");
        (
            await _room.SaveAsync<UpdateTriggerMessage>(
                CALLER_TRIGGER,
                stuffIds: [CLICK_ME],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ]
            )
        ).Should().BeTrue();

        // The caller's own condition: the negative call is its else branch.
        _room.AddBox<WiredConditionSelectQuantity>(CALLER_CONDITION, 0, 0, "wf_cnd_slc_quantity");
        (
            await _room.SaveAsync<UpdateConditionMessage>(
                CALLER_CONDITION,
                intParams: [0, callerPasses ? 0 : 5, 1],
                furniSources:
                [
                    [WiredFurniSourceType.SelectorItems],
                ],
                playerSources:
                [
                    [WiredPlayerSourceType.SelectorUsers],
                ]
            )
        ).Should().BeTrue();

        // The call picks the callee's trigger, so the callee is placed first.
        if (negative)
            _room.AddBox<WiredActionNegativeCallStacks>(
                CALLER_ACTION,
                0,
                0,
                "wf_act_neg_call_stacks"
            );
        else
            _room.AddBox<WiredActionCallStacks>(CALLER_ACTION, 0, 0, "wf_act_call_stacks");

        (
            await _room.SaveAsync<UpdateActionMessage>(
                CALLER_ACTION,
                stuffIds: [CALLEE_TRIGGER],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ],
                definitionSpecifics: [0]
            )
        ).Should().BeTrue();

        await Wired.OnRoomEventAsync(
            new RoomWiredStackChangedEvent
            {
                RoomId = 1,
                CausedBy = ActionContext.CreateForSystem(1),
                StackIds = [_room.Map.ToIdx(0, 0), _room.Map.ToIdx(2, 0)],
            },
            Ct
        );
        await TickAsync(1);
    }

    private Task ClickAsync() =>
        _room
            .FloorItem(CLICK_ME)
            .Logic.OnClickAsync(ActionContext.CreateForPlayer((PlayerId)105, (RoomId)1), 0, Ct);

    private int Logged(string text) => Wired.GetRoomLogPage(1, 10_000, -1, -1, text).TotalEntries;

    private async Task TickAsync(int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            _now += 1_000;
            await Wired.ProcessWiredAsync(_now, dormant: false, Ct);
        }
    }
}
