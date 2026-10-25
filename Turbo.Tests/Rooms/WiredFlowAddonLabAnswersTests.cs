using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Conditions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Selectors;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;
using Turbo.Rooms.Wired;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// The flow add-ons as the W4 lab measured them on an official room (official client capture
/// 2026-10-24, WIN63-202610201032); each test is named by its case id. A user clicks a furni;
/// the stack's effects toggle the lamps 30 to 33, so each click shows which effects ran. One user
/// is in the room: a user-count condition from 1 holds, one from 5 does not.
/// </summary>
public sealed class WiredFlowAddonLabAnswersTests
{
    private const int CLICK_ME = 20;
    private const int TRIGGER = 1;
    private static readonly int[] LAMPS = [30, 31, 32, 33];

    private readonly WiredRoom _room = new(8, 8);
    private long _now = 10_000;
    private int _z;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private RoomWiredSystem Wired => _room.Harness.Module<RoomWiredSystem>();

    public WiredFlowAddonLabAnswersTests()
    {
        _room.AddFloorItem(CLICK_ME, 7, 4);

        foreach (var lamp in LAMPS)
            _room.AddFloorItem(lamp, lamp - 27, 6);

        _room.Enter(5, 1, 1);
    }

    // addon.0.R3: an or_eval over no conditions reads its mode as written.
    [Theory]
    [InlineData(0, true)]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(3, true)]
    public async Task Addon0R3_WithNoConditionsAllAndNoneRunAtLeastOneAndNotAllDoNot(
        int mode,
        bool runs
    )
    {
        await BoxAsync<WiredAddonConditionsEval>(10, "wf_xtra_or_eval", [mode, 0, 0]);
        await ToggleAsync(40, 30);
        await ActivateAsync();

        (await ClickAsync()).Count.Should().Be(runs ? 1 : 0);
    }

    // addon.0.R1: one or_eval over a passing and a failing condition.
    [Theory]
    [InlineData(new[] { 1, 0, 0 }, true)]
    [InlineData(new[] { 0, 0, 0 }, false)]
    [InlineData(new[] { 2, 0, 0 }, true)]
    [InlineData(new[] { 3, 0, 0 }, false)]
    [InlineData(new[] { -1, 1, 1 }, true)]
    public async Task Addon0R1_OneOrEvalOverAPassingAndAFailingCondition(int[] eval, bool runs)
    {
        await CountAsync(3, 1);
        await CountAsync(4, 5);
        await BoxAsync<WiredAddonConditionsEval>(10, "wf_xtra_or_eval", eval);
        await ToggleAsync(40, 30);
        await ActivateAsync();

        (await ClickAsync()).Count.Should().Be(runs ? 1 : 0);
    }

    // addon.0.R2: two or_evals must both hold. "At least one" alone would run over a passing and
    // a failing condition, "all" alone would run over none.
    [Theory]
    [InlineData(1, 0, true)]
    [InlineData(1, 0, false)]
    [InlineData(1, 3, false)]
    public async Task Addon0R2_TwoOrEvalsMustBothHold(int first, int second, bool withConditions)
    {
        if (withConditions)
        {
            await CountAsync(3, 1);
            await CountAsync(4, 5);
        }

        await BoxAsync<WiredAddonConditionsEval>(10, "wf_xtra_or_eval", [first, 0, 0]);
        await BoxAsync<WiredAddonConditionsEval>(11, "wf_xtra_or_eval", [second, 0, 0]);
        await ToggleAsync(40, 30);
        await ActivateAsync();

        (await ClickAsync()).Should().BeEmpty();
    }

    // addon.0.R2: two rules that both hold still run.
    [Fact]
    public async Task Addon0R2_TwoOrEvalsThatBothHoldRun()
    {
        await CountAsync(3, 1);
        await CountAsync(4, 5);
        await BoxAsync<WiredAddonConditionsEval>(10, "wf_xtra_or_eval", [1, 0, 0]);
        await BoxAsync<WiredAddonConditionsEval>(11, "wf_xtra_or_eval", [2, 0, 0]);
        await ToggleAsync(40, 30);
        await ActivateAsync();

        (await ClickAsync()).Should().Equal(30);
    }

    // addon.5.R2: [1,20] and [3,20] on one stack let 4 clicks in 10 s run and block the 5th.
    [Fact]
    public async Task Addon5R2_TwoExecutionLimitsAddUp()
    {
        await BoxAsync<WiredAddonExecutionLimit>(10, "wf_xtra_execution_limit", [1, 20]);
        await BoxAsync<WiredAddonExecutionLimit>(11, "wf_xtra_execution_limit", [3, 20]);
        await ToggleAsync(40, 30);
        await ActivateAsync();

        (await RunsOfClicksAsync(5)).Should().Equal(true, true, true, true, false);

        _room.Clock.AdvanceMs(10_000);
        (await ClickAsync()).Should().Equal(30);
    }

    // addon.5.R2 / R1: one limit alone keeps its own count.
    [Theory]
    [InlineData(1, new[] { true, false, false })]
    [InlineData(3, new[] { true, true, true, false })]
    public async Task Addon5R2_OneExecutionLimitAlone(int executions, bool[] runs)
    {
        await BoxAsync<WiredAddonExecutionLimit>(10, "wf_xtra_execution_limit", [executions, 20]);
        await ToggleAsync(40, 30);
        await ActivateAsync();

        (await RunsOfClicksAsync(runs.Length)).Should().Equal(runs);
    }

    // addon.2.R1: one effect per firing in box-id order, whatever the stacking order. Box 42 is
    // stacked below 40 and 41.
    [Fact]
    public async Task Addon2R1_UnseenCyclesInBoxIdOrderNotStackingOrder()
    {
        await BoxAsync<WiredAddonUnseenActions>(10, "wf_xtra_unseen", []);
        await ToggleAsync(42, 32);
        await ToggleAsync(40, 30);
        await ToggleAsync(41, 31);
        await ActivateAsync();

        var ran = new List<int>();

        for (var i = 0; i < 6; i++)
            ran.AddRange(await ClickAsync());

        ran.Should().Equal(30, 31, 32, 30, 31, 32);
    }

    // addon.2.R2: two unseen add-ons share one cycle, still one effect per firing.
    [Fact]
    public async Task Addon2R2_TwoUnseenAddonsShareOneCycle()
    {
        await BoxAsync<WiredAddonUnseenActions>(10, "wf_xtra_unseen", []);
        await BoxAsync<WiredAddonUnseenActions>(11, "wf_xtra_unseen", []);

        foreach (var lamp in LAMPS)
            await ToggleAsync(lamp + 10, lamp);

        await ActivateAsync();

        var ran = new List<int>();

        for (var i = 0; i < 6; i++)
            ran.AddRange(await ClickAsync());

        ran.Should().Equal(30, 31, 32, 33, 30, 31);
    }

    // addon.1.R1: random picks one of the effects; the selectors still run, so an effect acting
    // on the selection does its work when drawn.
    [Fact]
    public async Task Addon1R1_RandomPicksAmongEffectsAndSelectorsStillRun()
    {
        await BoxAsync<WiredAddonRandomActions>(10, "wf_xtra_random", [0, 1]);
        await PicksAsync(15, [33]);
        await ToggleAsync(40, 30);
        await ToggleAsync(41, 31);
        await ToggleAsync(42, null);
        await ActivateAsync();

        var drawn = new HashSet<int>();

        for (var i = 0; i < 40; i++)
        {
            var ran = await ClickAsync();

            ran.Should().ContainSingle();
            drawn.Add(ran[0]);
        }

        drawn.Should().BeEquivalentTo([30, 31, 33]);
    }

    // addon.1.R2: two random add-ons still run exactly one effect per firing.
    [Fact]
    public async Task Addon1R2_TwoRandomAddonsStillRunOneEffect()
    {
        await BoxAsync<WiredAddonRandomActions>(10, "wf_xtra_random", [0, 1]);
        await BoxAsync<WiredAddonRandomActions>(11, "wf_xtra_random", [0, 1]);
        await ToggleAsync(40, 30);
        await ToggleAsync(41, 31);
        await ToggleAsync(42, 32);
        await ActivateAsync();

        for (var i = 0; i < 12; i++)
            (await ClickAsync()).Should().ContainSingle();
    }

    // addon.10.R3: the furni filter narrows a selector's result, not an effect's own picks.
    [Fact]
    public async Task Addon10R3_TheFurniFilterLeavesAnEffectsOwnPicks()
    {
        await BoxAsync<WiredAddonFurniSelectorFilter>(10, "wf_xtra_filter_furni", [1, 0, 1]);
        await SaveAsync<WiredActionToggleItemState, UpdateActionMessage>(
            40,
            "wf_act_toggle_state",
            [0],
            stuffIds: [30, 31],
            furniSources:
            [
                [WiredFurniSourceType.SelectedItems],
            ],
            definitionSpecifics: [0]
        );
        await ActivateAsync();

        (await ClickAsync()).Should().Equal(30, 31);
    }

    // addon.10.R1: amount 2 keeps 2 random entries of the selection, listed in its own order.
    [Fact]
    public async Task Addon10R1_TheFilterKeepsRandomEntriesInTheSelectionsOrder()
    {
        var filter = await BoxAsync<WiredAddonFurniSelectorFilter>(
            10,
            "wf_xtra_filter_furni",
            [2, 0, 1]
        );
        var kept = new HashSet<int>();

        for (var i = 0; i < 40; i++)
        {
            var ctx = Context();

            ctx.SelectorPool.SelectedFurniIds.UnionWith(LAMPS);
            await filter.MutatePolicyAsync(ctx, Ct);

            ctx.SelectorPool.SelectedFurniIds.Should().HaveCount(2).And.BeInAscendingOrder();
            kept.UnionWith(ctx.SelectorPool.SelectedFurniIds);
        }

        kept.Should().BeEquivalentTo(LAMPS, "every firing picks again at random");
    }

    // addon.10.R2: two filters on one stack, amount 1 and 2, keep 1.
    [Fact]
    public async Task Addon10R2_TwoFiltersTheSmallerWins()
    {
        var one = await BoxAsync<WiredAddonFurniSelectorFilter>(
            10,
            "wf_xtra_filter_furni",
            [1, 0, 1]
        );
        var two = await BoxAsync<WiredAddonFurniSelectorFilter>(
            11,
            "wf_xtra_filter_furni",
            [2, 0, 1]
        );

        foreach (var order in new[] { new[] { one, two }, new[] { two, one } })
        {
            var ctx = Context();

            ctx.SelectorPool.SelectedFurniIds.UnionWith(LAMPS);

            foreach (var filter in order)
                await filter.MutatePolicyAsync(ctx, Ct);

            ctx.SelectorPool.SelectedFurniIds.Should().ContainSingle();
        }
    }

    // addon.11.R3: the user filter with no user selector leaves the selection empty.
    [Fact]
    public async Task Addon11R3_TheUserFilterWithNoSelectorSelectsNobody()
    {
        var filter = await BoxAsync<WiredAddonUserSelectorFilter>(
            10,
            "wf_xtra_filter_users",
            [1, 0, 1]
        );
        var ctx = Context();

        await filter.MutatePolicyAsync(ctx, Ct);

        ctx.SelectorPool.SelectedAvatarIds.Should().BeEmpty();
    }

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

    private async Task<T> BoxAsync<T>(int id, string logicName, int[] intParams)
        where T : class, IRoomObjectLogic
    {
        var box = await SaveAsync<T, UpdateAddonMessage>(id, logicName, intParams);

        return box;
    }

    private async Task CountAsync(int id, int min) =>
        await SaveAsync<WiredConditionRoomHabboCount, UpdateConditionMessage>(
            id,
            "wf_cnd_user_count_in",
            [min, 50]
        );

    private async Task PicksAsync(int id, int[] furni) =>
        await SaveAsync<WiredSelectorSelectedItems, UpdateSelectorMessage>(
            id,
            "wf_slc_furni_picks",
            [],
            stuffIds: furni,
            definitionSpecifics: [false, false]
        );

    /// <summary>A toggle of one lamp, or of the selection when <paramref name="lamp"/> is null.</summary>
    private async Task ToggleAsync(int id, int? lamp) =>
        await SaveAsync<WiredActionToggleItemState, UpdateActionMessage>(
            id,
            "wf_act_toggle_state",
            [0],
            stuffIds: lamp is { } picked ? [picked] : [],
            furniSources:
            [
                [
                    lamp is null
                        ? WiredFurniSourceType.SelectorItems
                        : WiredFurniSourceType.SelectedItems,
                ],
            ],
            definitionSpecifics: [0]
        );

    /// <summary>
    /// A box stacked on the stack tile above the ones before it, saved as the editor sends it.
    /// </summary>
    private async Task<T> SaveAsync<T, TMessage>(
        int id,
        string logicName,
        int[] intParams,
        int[]? stuffIds = null,
        WiredFurniSourceType[][]? furniSources = null,
        object[]? definitionSpecifics = null
    )
        where T : class, IRoomObjectLogic
        where TMessage : UpdateWiredMessage
    {
        var box = _room.AddBox<T>(id, 0, 0, logicName);

        _room.FloorItem(id).SetPositionZ(Altitude.FromInt(_z++));

        (
            await _room.SaveAsync<TMessage>(
                id,
                intParams: intParams,
                stuffIds: stuffIds,
                furniSources: furniSources,
                definitionSpecifics: definitionSpecifics
            )
        )
            .Should()
            .BeTrue($"box {id} saves");

        return box;
    }

    private async Task ActivateAsync()
    {
        await SaveAsync<WiredTriggerClickFurni, UpdateTriggerMessage>(
            TRIGGER,
            "wf_trg_click_furni",
            [],
            stuffIds: [CLICK_ME],
            furniSources:
            [
                [WiredFurniSourceType.SelectedItems],
            ]
        );

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

    /// <summary>One click; the lamps it toggled, in id order.</summary>
    private async Task<List<int>> ClickAsync()
    {
        var before = LAMPS.ToDictionary(x => x, x => _room.FloorItem(x).Logic.GetState());

        await _room
            .FloorItem(CLICK_ME)
            .Logic.OnClickAsync(ActionContext.CreateForPlayer((PlayerId)105, (RoomId)1), 0, Ct);
        await TickAsync(3);

        return [.. LAMPS.Where(x => _room.FloorItem(x).Logic.GetState() != before[x])];
    }

    private async Task<List<bool>> RunsOfClicksAsync(int clicks)
    {
        var runs = new List<bool>();

        for (var i = 0; i < clicks; i++)
            runs.Add((await ClickAsync()).Count > 0);

        return runs;
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
