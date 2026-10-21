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
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Variables;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// "WIRED Variable: Pointer Variable" (<c>wf_var_pointer</c>, Wired Faculty 19/10/2026): "Configure a
/// Context Variable of choice in the Pointer variable, in which you will store the furni ID of a
/// variable box of choice. The pointer variable will now act like that other variable in
/// disguise." A click stack gives the context variable "target" the furni id of a global's box
/// and signals a second stack, which sets the global pointer "p" to 42 (a signalled stack shares
/// its sender's context variables): the global the id names takes the value.
/// </summary>
public sealed class WiredVariablePointerTests
{
    private const int CLICK_ME = 23;
    private const int ANTENNA = 21;
    private const int COINS = 10;
    private const int GEMS = 13;
    private const int CONTEXT = 11;
    private const int POINTER = 12;

    private readonly WiredRoom _room = new(8, 8);
    private long _now = 10_000;
    private WiredVariableRoom _coins = null!;
    private WiredVariableRoom _gems = null!;
    private WiredVariablePointer _pointer = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private RoomWiredSystem Wired => _room.Harness.Module<RoomWiredSystem>();

    public WiredVariablePointerTests()
    {
        _room.Enter(5, 1, 1);
        _room.AddFloorItem(CLICK_ME, 7, 4);
        _room.AddFloorItem(ANTENNA, 6, 1, "wf_antenna1");
    }

    /// <summary>
    /// Writes through a pointer change nothing: on Habbo, Change Variable "assign 7" through a
    /// pointer whose context variable held the target box's furni id left the target as it was,
    /// for a furni and a global pointer alike, while a direct write from the same box worked
    /// (official client capture 2026-10-20, lab61 (b) 4/4 and lab60 4/4, NOTES-lab61-20261020.md).
    /// </summary>
    [Theory]
    [InlineData(COINS)]
    [InlineData(GEMS)]
    public async Task A_write_through_a_pointer_changes_nothing(int pointsAt)
    {
        await BuildAsync(pointsAt);

        await ClickAsync();
        await TickAsync(6);

        Read(_coins).Should().Be(0);
        Read(_gems).Should().Be(0);
    }

    /// <summary>
    /// Reads through a pointer reach the variable whose box id the context variable holds (lab61
    /// (a)/(b): global pointer read 5 of g=5 3/3, furni pointer read 42 2/2): coins holds 5, the
    /// signalled stack sets gems to the pointer's value.
    /// </summary>
    [Fact]
    public async Task A_pointer_reads_the_variable_whose_box_id_the_context_variable_holds()
    {
        await BuildAsync(pointsAt: COINS, readIntoGems: true);
        (
            await _coins.SetValueAsync(
                _room.Harness.Fakes.Create<Turbo.Primitives.Rooms.Wired.IWiredExecutionContext>(),
                new WiredVariableKey(
                    _coins.GetVarSnapshot().VariableId,
                    WiredVariableTargetType.Global,
                    0
                ),
                new WiredVariableValue(5)
            )
        )
            .Should()
            .BeTrue();

        await ClickAsync();
        await TickAsync(6);

        Read(_gems).Should().Be(5);
        Read(_coins).Should().Be(5);
    }

    /// <summary>
    /// A context variable nobody gave holds no value, so Change Variable cannot fill it and the
    /// pointer reads nothing: on Habbo, with the context variable set by Change Variable instead of
    /// Give Variable, it read "null" and the pointer's placeholder stayed literal (official client
    /// capture 2026-10-20, lab61 (a) 3/3, NOTES-lab61-20261020.md).
    /// </summary>
    [Fact]
    public async Task A_pointer_whose_context_variable_was_only_changed_reads_nothing()
    {
        await BuildAsync(pointsAt: COINS, readIntoGems: true, fillWithChange: true);
        (
            await _coins.SetValueAsync(
                _room.Harness.Fakes.Create<Turbo.Primitives.Rooms.Wired.IWiredExecutionContext>(),
                new WiredVariableKey(
                    _coins.GetVarSnapshot().VariableId,
                    WiredVariableTargetType.Global,
                    0
                ),
                new WiredVariableValue(5)
            )
        )
            .Should()
            .BeTrue();

        await ClickAsync();
        await TickAsync(6);

        Read(_gems).Should().Be(0);
    }

    [Fact]
    public async Task A_pointer_at_no_variable_box_changes_nothing()
    {
        await BuildAsync(pointsAt: CLICK_ME);

        await ClickAsync();
        await TickAsync(6);

        Read(_coins).Should().Be(0);
        Read(_gems).Should().Be(0);
    }

    [Fact]
    public async Task The_pointer_is_listed_as_its_kind_and_requires_an_execution_context()
    {
        await BuildAsync(pointsAt: COINS);

        var snapshot = _pointer.GetVarSnapshot();

        snapshot.VariableName.Should().Be("p");
        snapshot.TargetType.Should().Be(WiredVariableTargetType.Global);
        snapshot.Flags.Has(WiredVariableFlags.RequiresExecutionContext).Should().BeTrue();
        _pointer.WiredCode.Should().Be(9);

        // Outside a running stack there is no context variable to name the target.
        _pointer
            .TryGetValue(
                new WiredVariableKey(snapshot.VariableId, WiredVariableTargetType.Global, 0),
                out _
            )
            .Should()
            .BeFalse();
    }

    /// <summary>A fresh pointer opens on users (official client capture lab60, WIN63-202610201032: ints [1]).</summary>
    [Fact]
    public async Task A_fresh_pointer_stands_for_a_user_variable()
    {
        var pointer = _room.AddBox<WiredVariablePointer>(POINTER, 2, 2, "wf_var_pointer");

        await pointer.LoadWiredAsync(Ct);

        pointer.GetSnapshot().IntParams.Should().Equal(1);
        pointer.GetSnapshot().DefaultIntParams.Should().Equal(1);
    }

    private static long Read(WiredVariableRoom variable)
    {
        var snapshot = variable.GetVarSnapshot();

        variable
            .TryGetValue(
                new WiredVariableKey(snapshot.VariableId, WiredVariableTargetType.Global, 0),
                out var value
            )
            .Should()
            .BeTrue();

        return value.Value;
    }

    private async Task BuildAsync(
        int pointsAt,
        bool readIntoGems = false,
        bool fillWithChange = false
    )
    {
        _coins = await AddGlobalAsync(COINS, 4, 4, "coins");
        _gems = await AddGlobalAsync(GEMS, 5, 5, "gems");

        var context = _room.AddBox<WiredVariableContext>(CONTEXT, 3, 3, "wf_var_context");

        (
            await _room.SaveAsync<UpdateVariableMessage>(
                CONTEXT,
                intParams: [1],
                stringParam: "target"
            )
        )
            .Should()
            .BeTrue();
        await context.LoadWiredAsync(Ct);
        await StartAsync(3, 3);

        var contextId = context.GetVarSnapshot().VariableId.ToString();

        _pointer = _room.AddBox<WiredVariablePointer>(POINTER, 2, 2, "wf_var_pointer");

        (
            await _room.SaveAsync<UpdateVariableMessage>(
                POINTER,
                intParams: [(int)WiredVariableTargetType.Global],
                stringParam: "p",
                variableIds: [contextId]
            )
        )
            .Should()
            .BeTrue();
        await _pointer.LoadWiredAsync(Ct);
        await StartAsync(2, 2);

        var pointerId = _pointer.GetVarSnapshot().VariableId.ToString();

        _room.AddBox<WiredTriggerClickFurni>(1, 0, 0, "wf_trg_click_furni");
        if (fillWithChange)
            _room.AddBox<WiredActionChangeVariable>(2, 0, 0, "wf_act_change_var_val");
        else
            _room.AddBox<WiredActionGiveVariable>(2, 0, 0, "wf_act_give_var");
        _room.AddBox<WiredActionSendSignal>(3, 0, 0, "wf_act_send_signal");
        _room.AddBox<WiredTriggerReceiveSignal>(4, 0, 4, "wf_trg_recv_signal");
        _room.AddBox<WiredActionChangeVariable>(5, 0, 4, "wf_act_change_var_val");

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
        // Give the context variable the furni id of the box to point at (or set it, ungiven).
        (
            await _room.SaveAsync<UpdateActionMessage>(
                2,
                intParams: fillWithChange
                    ?
                    [
                        (int)WiredVariableTargetType.Context,
                        (int)WiredVariableOperationType.Set,
                        0,
                        0,
                        pointsAt,
                        (int)WiredVariableTargetType.Global,
                    ]
                    : [(int)WiredVariableTargetType.Context, 0, pointsAt, 1],
                definitionSpecifics: [0],
                variableIds: [contextId]
            )
        )
            .Should()
            .BeTrue();
        (
            await _room.SaveAsync<UpdateActionMessage>(
                3,
                intParams: [0, 0],
                stuffIds: [ANTENNA],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                    [WiredFurniSourceType.SelectedItems],
                ],
                playerSources:
                [
                    [WiredPlayerSourceType.TriggeredUser],
                ],
                definitionSpecifics: [0]
            )
        ).Should().BeTrue();
        (
            await _room.SaveAsync<UpdateTriggerMessage>(
                4,
                stuffIds: [ANTENNA],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ]
            )
        ).Should().BeTrue();
        // Set the pointer to 42, or gems to the pointer's value.
        (
            await _room.SaveAsync<UpdateActionMessage>(
                5,
                intParams:
                [
                    (int)WiredVariableTargetType.Global,
                    (int)WiredVariableOperationType.Set,
                    readIntoGems ? 1 : 0,
                    0,
                    readIntoGems ? 0 : 42,
                    (int)WiredVariableTargetType.Global,
                ],
                definitionSpecifics: [0],
                variableIds: readIntoGems
                    ? [_gems.GetVarSnapshot().VariableId.ToString(), pointerId]
                    : [pointerId]
            )
        )
            .Should()
            .BeTrue();

        await StartAsync(0, 0);
        await StartAsync(0, 4);
    }

    private async Task<WiredVariableRoom> AddGlobalAsync(int id, int x, int y, string name)
    {
        var global = _room.AddBox<WiredVariableRoom>(id, x, y, "wf_var_room");

        (
            await _room.SaveAsync<UpdateVariableMessage>(
                id,
                intParams: [(int)WiredAvailabilityType.RoomActive],
                stringParam: name
            )
        )
            .Should()
            .BeTrue();
        await global.LoadWiredAsync(Ct);
        await StartAsync(x, y);

        return global;
    }

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
