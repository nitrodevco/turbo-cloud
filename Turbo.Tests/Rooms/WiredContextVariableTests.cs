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
/// A context variable lives in one wired execution (Wired Faculty variables-info #13): a stack gives
/// it a value its own effects can read, and a stack it signals starts from a copy of it ("Memorization
/// with Signals", #15). Stack one, on (0,0): a click, "give variable" (context, 7) and "send signal".
/// Stack two, on (0,4): "receive signal" and "change variable value" setting a global to the context
/// variable. The global shows what the second stack read.
/// </summary>
public sealed class WiredContextVariableTests
{
    private const int CLICK_ME = 23;
    private const int ANTENNA = 21;
    private const int GLOBAL = 10;
    private const int CONTEXT = 11;

    private readonly WiredRoom _room = new(8, 8);
    private long _now = 10_000;
    private WiredVariableRoom _global = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private RoomWiredSystem Wired => _room.Harness.Module<RoomWiredSystem>();

    public WiredContextVariableTests()
    {
        _room.Enter(5, 1, 1);
        _room.AddFloorItem(CLICK_ME, 7, 4);
        _room.AddFloorItem(ANTENNA, 6, 1);
    }

    [Fact]
    public async Task A_signalled_stack_reads_the_context_value_its_sender_gave()
    {
        await BuildAsync();

        await ClickAsync();
        await TickAsync(6);

        Global().Should().Be(7);
    }

    private int Global()
    {
        var snapshot = _global.GetVarSnapshot();

        _global
            .TryGetValue(
                new WiredVariableKey(snapshot.VariableId, WiredVariableTargetType.Global, 0),
                out var value
            )
            .Should()
            .BeTrue();

        return value;
    }

    private async Task BuildAsync()
    {
        _global = _room.AddBox<WiredVariableRoom>(GLOBAL, 4, 4, "wf_var_room");
        var context = _room.AddBox<WiredVariableContext>(CONTEXT, 3, 3, "wf_var_context");

        (
            await _room.SaveAsync<UpdateVariableMessage>(
                GLOBAL,
                intParams: [(int)WiredAvailabilityType.RoomActive],
                stringParam: "copied"
            )
        )
            .Should()
            .BeTrue();
        (
            await _room.SaveAsync<UpdateVariableMessage>(
                CONTEXT,
                intParams: [1],
                stringParam: "carried"
            )
        )
            .Should()
            .BeTrue();
        await _global.LoadWiredAsync(Ct);
        await context.LoadWiredAsync(Ct);
        await StartAsync(4, 4);
        await StartAsync(3, 3);

        var globalId = _global.GetVarSnapshot().VariableId.ToString();
        var contextId = context.GetVarSnapshot().VariableId.ToString();

        _room.AddBox<WiredTriggerClickFurni>(1, 0, 0, "wf_trg_click_furni");
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
        (
            await _room.SaveAsync<UpdateActionMessage>(
                2,
                intParams: [(int)WiredVariableTargetType.Context, 0, 7, 1],
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
        // Set the global to the context variable's value.
        (
            await _room.SaveAsync<UpdateActionMessage>(
                5,
                intParams:
                [
                    (int)WiredVariableTargetType.Global,
                    (int)WiredVariableOperationType.Set,
                    1,
                    0,
                    0,
                    (int)WiredVariableTargetType.Context,
                ],
                definitionSpecifics: [0],
                variableIds: [globalId, contextId]
            )
        )
            .Should()
            .BeTrue();

        await StartAsync(0, 0);
        await StartAsync(0, 4);
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
