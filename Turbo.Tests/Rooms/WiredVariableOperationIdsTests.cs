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
/// "Change Variable Value" with the operation ids its editor offers
/// (<c>wiredfurni.params.variables.operation.&lt;id&gt;</c>): 5 Power, 6 Modulo, 60 Absolute value,
/// 110 Bit count (AS3 <c>ChangeVariable.operatorOptions</c>; the last two take no operand), and from
/// 111 the bit operations whose operand is a position: next / previous low (0) or high (1) bit,
/// inclusive (111-114) or exclusive (119-122), -1 when there is none, and get / set / clear /
/// toggle bit (115-118), as the Wired Faculty's "Intro to Bitwise Operations" explains them. A click
/// sets a global to 13 and then applies the operation, in order.
/// </summary>
public sealed class WiredVariableOperationIdsTests
{
    private const int CLICK_ME = 23;
    private const int VARIABLE = 10;

    private readonly WiredRoom _room = new(8, 8);
    private long _now = 10_000;
    private WiredVariableRoom _variable = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private RoomWiredSystem Wired => _room.Harness.Module<RoomWiredSystem>();

    public WiredVariableOperationIdsTests()
    {
        _room.Enter(5, 1, 1);
        _room.AddFloorItem(CLICK_ME, 7, 4);
    }

    [Theory]
    [InlineData(5, 2, 169)]
    [InlineData(6, 5, 3)]
    [InlineData(60, 0, 13)]
    [InlineData(110, 0, 3)]
    // 13 is 1101: the bits at positions 0, 2 and 3 are set.
    [InlineData(111, 4, 4)]
    [InlineData(112, 1, 2)]
    [InlineData(113, 3, 1)]
    [InlineData(114, 1, 0)]
    [InlineData(115, 2, 1)]
    [InlineData(116, 1, 15)]
    [InlineData(117, 2, 9)]
    [InlineData(118, 0, 12)]
    [InlineData(120, 0, 2)]
    [InlineData(122, 0, -1)]
    public async Task Each_operation_id_does_what_its_editor_label_says(
        int operation,
        int operand,
        int expected
    )
    {
        await BuildAsync(operation, operand);

        await ClickAsync();
        await TickAsync(4);

        Value().Should().Be(expected);
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

        return value;
    }

    private async Task BuildAsync(int operation, int operand)
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
        _room.AddBox<WiredActionChangeVariable>(3, 0, 0, "wf_act_change_var_val");
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

        await SaveChangeAsync(2, (int)WiredVariableOperationType.Set, 13, variableId);
        await SaveChangeAsync(3, operation, operand, variableId);

        await StartAsync(0, 0);
    }

    /// <summary>Target global, the operation, a literal operand (low word), operand target global.</summary>
    private async Task SaveChangeAsync(int boxId, int operation, int operand, string variableId) =>
        (
            await _room.SaveAsync<UpdateActionMessage>(
                boxId,
                intParams:
                [
                    (int)WiredVariableTargetType.Global,
                    operation,
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
