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
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Variables;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// Several "Change Variable Value" boxes on one stack. Official wired makes them one change unless
/// "Execute In Order" is on, applying assignment, power, multiplication, division, modulo, addition
/// and subtraction in that order whatever the stacking order (sirjonasxx, Wired Faculty
/// variables-info #14). The stack here, bottom to top: set 3, add 5, multiply by 2 - run in stacking
/// order that is (3 + 5) * 2 = 16; combined it is 3 * 2 + 5 = 11.
/// </summary>
public sealed class WiredVariableChangeOrderTests
{
    private const int CLICK_ME = 23;
    private const int VARIABLE = 10;

    private readonly WiredRoom _room = new(8, 8);
    private long _now = 10_000;
    private WiredVariableRoom _variable = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private RoomWiredSystem Wired => _room.Harness.Module<RoomWiredSystem>();

    public WiredVariableChangeOrderTests()
    {
        _room.Enter(5, 1, 1);
        _room.AddFloorItem(CLICK_ME, 7, 4);
    }

    [Fact]
    public async Task A_global_variable_holds_zero_from_the_start_and_takes_a_change()
    {
        await BuildAsync(executeInOrder: true, onlyFirstBox: true);

        Value().Should().Be(0);

        await ClickAsync();
        await TickAsync(4);

        Value().Should().Be(3);
    }

    [Fact]
    public async Task Without_execute_in_order_the_changes_are_one_change_in_the_official_order()
    {
        await BuildAsync(executeInOrder: false);

        await ClickAsync();
        await TickAsync(4);

        Value().Should().Be(11);
    }

    [Fact]
    public async Task With_execute_in_order_each_box_changes_the_value_in_stacking_order()
    {
        await BuildAsync(executeInOrder: true);

        await ClickAsync();
        await TickAsync(4);

        Value().Should().Be(16);
    }

    private int Value()
    {
        var snapshot = _variable.GetVarSnapshot();

        _variable
            .TryGetValue(
                new WiredVariableKey(snapshot.VariableId, WiredVariableTargetType.Global, 0),
                out var value
            )
            .Should()
            .BeTrue();

        return (int)value;
    }

    private async Task BuildAsync(bool executeInOrder, bool onlyFirstBox = false)
    {
        _variable = _room.AddBox<WiredVariableRoom>(VARIABLE, 4, 4, "wf_var_room");
        (
            await _room.SaveAsync<UpdateVariableMessage>(
                VARIABLE,
                intParams: [(int)WiredAvailabilityType.RoomActive],
                stringParam: "score"
            )
        )
            .Should()
            .BeTrue();
        await _variable.LoadWiredAsync(Ct);
        await StartAsync(4, 4);

        var variableId = _variable.GetVarSnapshot().VariableId.ToString();

        _room.AddBox<WiredTriggerClickFurni>(1, 0, 0, "wf_trg_click_furni");
        _room.AddBox<WiredActionChangeVariable>(2, 0, 0, "wf_act_change_var_val");

        if (!onlyFirstBox)
        {
            _room.AddBox<WiredActionChangeVariable>(3, 0, 0, "wf_act_change_var_val");
            _room.AddBox<WiredActionChangeVariable>(4, 0, 0, "wf_act_change_var_val");
        }

        if (executeInOrder)
            _room.AddBox<WiredAddonExecuteInOrder>(5, 0, 0, "wf_xtra_exec_in_order");

        (
            await _room.SaveAsync<UpdateTriggerMessage>(
                1,
                stuffIds: [CLICK_ME],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ]
            )
        ).Should().BeTrue();

        await SaveChangeAsync(2, WiredVariableOperationType.Set, 3, variableId);

        if (!onlyFirstBox)
        {
            await SaveChangeAsync(3, WiredVariableOperationType.Add, 5, variableId);
            await SaveChangeAsync(4, WiredVariableOperationType.Multiply, 2, variableId);
        }

        await StartAsync(0, 0);
    }

    /// <summary>Target global, the operation, a literal operand (low word), operand target global.</summary>
    private async Task SaveChangeAsync(
        int boxId,
        WiredVariableOperationType operation,
        int operand,
        string variableId
    ) =>
        (
            await _room.SaveAsync<UpdateActionMessage>(
                boxId,
                intParams:
                [
                    (int)WiredVariableTargetType.Global,
                    (int)operation,
                    0,
                    0,
                    operand,
                    (int)WiredVariableTargetType.Global,
                ],
                definitionSpecifics: [0],
                variableIds: [variableId]
            )
        )
            .Should()
            .BeTrue();

    /// <summary>The room builds a tile's stack on the next tick after a box says it changed.</summary>
    private async Task StartAsync(int x, int y)
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

    private Task ClickAsync() =>
        _room
            .FloorItem(CLICK_ME)
            .Logic.OnClickAsync(ActionContext.CreateForPlayer((PlayerId)105, (RoomId)1), 0, Ct);

    private async Task TickAsync(int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            _now += 1_000;
            await Wired.ProcessWiredAsync(_now, dormant: false, Ct);
        }
    }
}
