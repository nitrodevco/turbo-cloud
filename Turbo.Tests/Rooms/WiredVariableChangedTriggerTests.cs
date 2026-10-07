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
/// "WIRED Trigger: Variable Changed" with the options its editor saves: created, value changed
/// (increased, decreased, unchanged) and deleted, and "Allow triggering from" (this room, another
/// room, inspection, external). Stack one, on (0,0): a click and a change to the global "counter".
/// Stack two, on (0,4): the trigger on "counter" and "add 1" to the global "hits", so "hits" counts
/// the firings.
/// </summary>
public sealed class WiredVariableChangedTriggerTests
{
    private const int CLICK_ME = 23;
    private const int COUNTER = 10;
    private const int HITS = 11;

    private const int INCREASED = 1;
    private const int UNCHANGED = 4;
    private const int ALL_ORIGINS = -1;
    private const int THIS_ROOM = 1;

    private readonly WiredRoom _room = new(8, 8);
    private long _now = 10_000;
    private WiredVariableRoom _counter = null!;
    private WiredVariableRoom _hits = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private RoomWiredSystem Wired => _room.Harness.Module<RoomWiredSystem>();

    public WiredVariableChangedTriggerTests()
    {
        _room.Enter(5, 1, 1);
        _room.AddFloorItem(CLICK_ME, 7, 4);
    }

    [Fact]
    public async Task Created_and_value_changed_fire_on_a_change_of_the_value()
    {
        // What the editor saves with "Created" and "Value changed" ticked: the old reading took
        // param 1 as a change-kind mask and never fired on an update.
        await BuildAsync(WiredVariableOperationType.Add, 5, [1, 1, 0, 0, ALL_ORIGINS]);

        await FireAsync();

        Hits().Should().Be(1);
    }

    [Fact]
    public async Task Increased_fires_on_an_increase()
    {
        await BuildAsync(WiredVariableOperationType.Add, 5, [0, 1, 0, INCREASED, ALL_ORIGINS]);

        await FireAsync();

        Hits().Should().Be(1);
    }

    [Fact]
    public async Task Increased_does_not_fire_on_a_decrease()
    {
        await BuildAsync(WiredVariableOperationType.Subtract, 5, [0, 1, 0, INCREASED, ALL_ORIGINS]);

        await FireAsync();

        Hits().Should().Be(0);
    }

    [Fact]
    public async Task Unchanged_fires_when_a_change_leaves_the_value_as_it_was()
    {
        await BuildAsync(WiredVariableOperationType.Add, 0, [0, 1, 0, UNCHANGED, ALL_ORIGINS]);

        await FireAsync();

        Hits().Should().Be(1);
    }

    [Fact]
    public async Task This_room_only_ignores_a_change_made_with_the_inspection_tool()
    {
        await BuildAsync(WiredVariableOperationType.Add, 5, [0, 1, 0, 0, THIS_ROOM]);

        var snapshot = _counter.GetVarSnapshot();

        (
            await Wired.ApplyVariableMenuOperationAsync(
                new WiredVariableBinding(WiredVariableTargetType.Global, 0),
                snapshot.VariableId,
                WiredVariableMenuOperationType.SetValue,
                9,
                Ct
            )
        )
            .Should()
            .BeTrue();
        await TickAsync(4);

        Hits().Should().Be(0);

        await FireAsync();

        Hits().Should().Be(1);
    }

    private async Task FireAsync()
    {
        await ClickAsync();
        await TickAsync(6);
    }

    private int Hits()
    {
        var snapshot = _hits.GetVarSnapshot();

        _hits
            .TryGetValue(
                new WiredVariableKey(snapshot.VariableId, WiredVariableTargetType.Global, 0),
                out var value
            )
            .Should()
            .BeTrue();

        return value;
    }

    private async Task<WiredVariableRoom> AddGlobalAsync(int id, int x, string name)
    {
        var box = _room.AddBox<WiredVariableRoom>(id, x, 6, "wf_var_room");

        (
            await _room.SaveAsync<UpdateVariableMessage>(
                id,
                intParams: [(int)WiredAvailabilityType.RoomActive],
                stringParam: name
            )
        )
            .Should()
            .BeTrue();
        await box.LoadWiredAsync(Ct);
        await StartAsync(x, 6);

        return box;
    }

    private async Task BuildAsync(
        WiredVariableOperationType operation,
        int operand,
        int[] triggerParams
    )
    {
        _counter = await AddGlobalAsync(COUNTER, 4, "counter");
        _hits = await AddGlobalAsync(HITS, 5, "hits");

        var counterId = _counter.GetVarSnapshot().VariableId.ToString();
        var hitsId = _hits.GetVarSnapshot().VariableId.ToString();

        _room.AddBox<WiredTriggerClickFurni>(1, 0, 0, "wf_trg_click_furni");
        _room.AddBox<WiredActionChangeVariable>(2, 0, 0, "wf_act_change_var_val");
        _room.AddBox<WiredTriggerVariableChanged>(3, 0, 4, "wf_trg_var_changed");
        _room.AddBox<WiredActionChangeVariable>(4, 0, 4, "wf_act_change_var_val");

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
        await SaveChangeAsync(2, operation, operand, counterId);
        (
            await _room.SaveAsync<UpdateTriggerMessage>(
                3,
                intParams: triggerParams,
                variableIds: [counterId]
            )
        )
            .Should()
            .BeTrue();
        await SaveChangeAsync(4, WiredVariableOperationType.Add, 1, hitsId);

        await StartAsync(0, 0);
        await StartAsync(0, 4);
    }

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
