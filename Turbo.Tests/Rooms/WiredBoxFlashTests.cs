using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Conditions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Selectors;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Variables;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// The activation flash of wired boxes, as Habbo shows it (recorded on hhus,
/// WIN63-202610121709, a public wired room running every 500 ms): a trigger flips each time it
/// fires, its conditions only when they pass, a selector between its resting state + 1 and + 2
/// (resting at 0, or 3 in filter mode), and a variable box between 2 and 3 when its value goes up
/// and between 4 and 5 when it goes down.
/// </summary>
public sealed class WiredBoxFlashTests
{
    private const int CLICK_ME = 20;
    private const int PAD = 21;
    private const int TRIGGER = 1;
    private const int CONDITION = 2;
    private const int EFFECT = 3;
    private const int SELECTOR = 4;
    private const int GLOBAL = 5;

    private readonly WiredRoom _room = new(8, 8);
    private long _now = 10_000;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private RoomWiredSystem Wired => _room.Harness.Module<RoomWiredSystem>();

    [Fact]
    public async Task A_trigger_flashes_every_time_and_its_condition_only_when_it_passes()
    {
        _room.AddFloorItem(CLICK_ME, 7, 4);
        _room.AddFloorItem(PAD, 3, 3);
        var trigger = _room.AddBox<WiredTriggerClickFurni>(TRIGGER, 0, 0, "wf_trg_click_furni");
        var condition = _room.AddBox<WiredConditionItemHasHabbo>(
            CONDITION,
            0,
            0,
            "wf_cnd_furnis_hv_avtrs"
        );
        _room.AddBox<WiredActionToggleItemState>(EFFECT, 0, 0, "wf_act_toggle_state");
        await SaveTriggerAsync();
        (
            await _room.SaveAsync<UpdateConditionMessage>(
                CONDITION,
                intParams: [0],
                stuffIds: [PAD],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ],
                definitionSpecifics: [0]
            )
        ).Should().BeTrue();
        (
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
        await StartAsync();

        // Nobody on the pad: the condition fails, yet the trigger fired.
        await ClickAsync();
        trigger.GetState().Should().Be(1);
        condition.GetState().Should().Be(0);

        _room.Enter(5, 3, 3);
        await ClickAsync();
        trigger.GetState().Should().Be(0);
        condition.GetState().Should().Be(1);
    }

    [Theory]
    [InlineData(false, 1, 2)]
    [InlineData(true, 4, 5)]
    public async Task A_selector_flashes_between_its_resting_state_plus_one_and_plus_two(
        bool filter,
        int first,
        int second
    )
    {
        _room.AddFloorItem(CLICK_ME, 7, 4);
        _room.AddFloorItem(PAD, 3, 3);
        _room.AddBox<WiredTriggerClickFurni>(TRIGGER, 0, 0, "wf_trg_click_furni");
        var selector = _room.AddBox<WiredSelectorItemsByType>(
            SELECTOR,
            0,
            0,
            "wf_slc_furni_bytype"
        );
        await SaveTriggerAsync();
        (
            await _room.SaveAsync<UpdateSelectorMessage>(
                SELECTOR,
                intParams: [0],
                stuffIds: [PAD],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ],
                definitionSpecifics: [filter, false]
            )
        ).Should().BeTrue();
        await StartAsync();

        await ClickAsync();
        selector.GetState().Should().Be(first);

        await ClickAsync();
        selector.GetState().Should().Be(second);

        await ClickAsync();
        selector.GetState().Should().Be(first);
    }

    [Fact]
    public async Task A_variable_box_flashes_2_3_while_its_value_rises_and_4_5_while_it_falls()
    {
        _room.AddFloorItem(CLICK_ME, 7, 4);
        _room.AddBox<WiredTriggerClickFurni>(TRIGGER, 0, 0, "wf_trg_click_furni");
        var global = _room.AddBox<WiredVariableRoom>(GLOBAL, 4, 4, "wf_var_room");
        (
            await _room.SaveAsync<UpdateVariableMessage>(
                GLOBAL,
                intParams: [(int)WiredAvailabilityType.RoomActive],
                stringParam: "count"
            )
        )
            .Should()
            .BeTrue();
        await global.LoadWiredAsync(Ct);
        await StartAsync(4, 4);
        var globalId = global.GetVarSnapshot().VariableId.ToString();

        _room.AddBox<WiredActionChangeVariable>(EFFECT, 0, 0, "wf_act_change_var_val");
        await SaveTriggerAsync();
        await SaveChangeAsync(globalId, WiredVariableOperationType.Add);
        await StartAsync();

        await ClickAsync();
        global.GetState().Should().Be(2);
        await ClickAsync();
        global.GetState().Should().Be(3);

        await SaveChangeAsync(globalId, WiredVariableOperationType.Subtract);
        await StartAsync();

        await ClickAsync();
        global.GetState().Should().Be(4);
        await ClickAsync();
        global.GetState().Should().Be(5);
    }

    // Wired matrix action.40.R3: Remove Variable flashes the variable box to 8, for a furni and a
    // user variable alike (official client capture 2026-10-25T01-22-35, 1/1 each; a give 6, a
    // change +1 2 in the same run).
    [Fact]
    public async Task A_variable_box_flashes_8_when_a_value_is_removed()
    {
        _room.AddFloorItem(CLICK_ME, 7, 4);
        _room.AddBox<WiredTriggerClickFurni>(TRIGGER, 0, 0, "wf_trg_click_furni");
        var furni = _room.AddBox<WiredVariableFurni>(GLOBAL, 4, 4, "wf_var_furni");
        (
            await _room.SaveAsync<UpdateVariableMessage>(
                GLOBAL,
                intParams: [(int)WiredAvailabilityType.RoomActive, 1],
                stringParam: "hp"
            )
        )
            .Should()
            .BeTrue();
        await furni.LoadWiredAsync(Ct);
        await StartAsync(4, 4);
        var furniId = furni.GetVarSnapshot().VariableId;

        _room.AddBox<WiredActionRemoveVariable>(EFFECT, 0, 0, "wf_act_remove_var");
        await SaveTriggerAsync();
        (
            await _room.SaveAsync<UpdateActionMessage>(
                EFFECT,
                intParams: [(int)WiredVariableTargetType.Furni],
                stuffIds: [CLICK_ME],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ],
                variableIds: [furniId.ToString()]
            )
        ).Should().BeTrue();
        await StartAsync();

        var key = new WiredVariableKey(furniId, WiredVariableTargetType.Furni, CLICK_ME);

        (await furni.GiveValueAsync(key, new WiredVariableValue(5))).Should().BeTrue();
        await TickAsync(1);
        furni.GetState().Should().Be(6);

        await ClickAsync();

        furni.TryGetValue(key, out _).Should().BeFalse();
        furni.GetState().Should().Be(8);
    }

    private async Task SaveChangeAsync(string variableId, WiredVariableOperationType operation) =>
        (
            await _room.SaveAsync<UpdateActionMessage>(
                EFFECT,
                intParams:
                [
                    (int)WiredVariableTargetType.Global,
                    (int)operation,
                    0,
                    0,
                    1,
                    (int)WiredVariableTargetType.Global,
                    0,
                ],
                definitionSpecifics: [0],
                variableIds: [variableId, "0"]
            )
        )
            .Should()
            .BeTrue();

    private async Task SaveTriggerAsync() => (
            await _room.SaveAsync<UpdateTriggerMessage>(
                TRIGGER,
                stuffIds: [CLICK_ME],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ]
            )
        ).Should().BeTrue();

    private async Task StartAsync(int x = 0, int y = 0)
    {
        await Wired.OnRoomEventAsync(
            new RoomWiredStackChangedEvent
            {
                RoomId = 1,
                CausedBy = ActionContext.CreateForSystem(1),
                StackIds = [_room.Map.ToIdx(x, y)],
            },
            Ct
        );
        await TickAsync(1);
    }

    private async Task ClickAsync()
    {
        await _room
            .FloorItem(CLICK_ME)
            .Logic.OnClickAsync(ActionContext.CreateForPlayer((PlayerId)105, (RoomId)1), 0, Ct);
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
}
