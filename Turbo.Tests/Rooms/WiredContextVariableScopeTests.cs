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
/// Wired Faculty variables-info #18, "Context Variables - Lifetime" (Sulake): a stack sets the
/// context variable "number" and signals two stacks, A and B. "A changes the value of number to
/// 20 ... since A modified number to 20, it will also be 20 for B", unless B gives its own with
/// "Override existing variable". Every signalled stack worked on a copy, so B still read the
/// sender's value.
/// </summary>
public sealed class WiredContextVariableScopeTests
{
    private const int CLICK_ME = 23;
    private const int ANTENNA_A = 21;
    private const int ANTENNA_B = 22;

    private readonly WiredRoom _room = new(10, 10);
    private long _now = 10_000;
    private WiredVariableRoom _seen = null!;
    private string _numberId = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private RoomWiredSystem Wired => _room.Harness.Module<RoomWiredSystem>();

    [Theory]
    [InlineData(false, 20)]
    [InlineData(true, 5)]
    public async Task Sibling_branches_share_a_context_variable_until_one_gives_its_own(
        bool bGivesItsOwn,
        int seenByB
    )
    {
        await BuildAsync(bGivesItsOwn);

        await _room
            .FloorItem(CLICK_ME)
            .Logic.OnClickAsync(ActionContext.CreateForPlayer((PlayerId)105, (RoomId)1), 0, Ct);
        await TickAsync(8);

        _seen
            .TryGetValue(
                new WiredVariableKey(
                    _seen.GetVarSnapshot().VariableId,
                    WiredVariableTargetType.Global,
                    0
                ),
                out var value
            )
            .Should()
            .BeTrue();
        value.Value.Should().Be(seenByB);
    }

    private async Task BuildAsync(bool bGivesItsOwn)
    {
        _room.Enter(5, 1, 1);
        _room.AddFloorItem(CLICK_ME, 9, 9);
        _room.AddFloorItem(ANTENNA_A, 8, 1);
        _room.AddFloorItem(ANTENNA_B, 8, 2);

        _seen = _room.AddBox<WiredVariableRoom>(30, 5, 5, "wf_var_room");
        var number = _room.AddBox<WiredVariableContext>(31, 5, 6, "wf_var_context");

        (
            await _room.SaveAsync<UpdateVariableMessage>(
                30,
                intParams: [(int)WiredAvailabilityType.RoomActive],
                stringParam: "seen"
            )
        )
            .Should()
            .BeTrue();
        (await _room.SaveAsync<UpdateVariableMessage>(31, intParams: [1], stringParam: "number"))
            .Should()
            .BeTrue();
        await _seen.LoadWiredAsync(Ct);
        await number.LoadWiredAsync(Ct);
        await StartAsync(5, 5);
        await StartAsync(5, 6);

        var seenId = _seen.GetVarSnapshot().VariableId.ToString();
        _numberId = number.GetVarSnapshot().VariableId.ToString();

        // The sender: number = 7, then a signal to both antennas.
        _room.AddBox<WiredTriggerClickFurni>(1, 0, 0, "wf_trg_click_furni");
        _room.AddBox<WiredActionGiveVariable>(2, 0, 0, "wf_act_give_var");
        _room.AddBox<WiredActionSendSignal>(3, 0, 0, "wf_act_send_signal");
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
        await GiveAsync(2, 7);
        (
            await _room.SaveAsync<UpdateActionMessage>(
                3,
                intParams: [0, 0],
                stuffIds: [ANTENNA_A, ANTENNA_B],
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

        // A: number + 13.
        _room.AddBox<WiredTriggerReceiveSignal>(4, 0, 4, "wf_trg_recv_signal");
        _room.AddBox<WiredActionChangeVariable>(5, 0, 4, "wf_act_change_var_val");
        await ReceiveAsync(4, ANTENNA_A);
        (
            await _room.SaveAsync<UpdateActionMessage>(
                5,
                intParams:
                [
                    (int)WiredVariableTargetType.Context,
                    (int)WiredVariableOperationType.Add,
                    0,
                    0,
                    13,
                    (int)WiredVariableTargetType.Global,
                ],
                definitionSpecifics: [0],
                variableIds: [_numberId]
            )
        )
            .Should()
            .BeTrue();

        // B: (optionally its own number = 5,) then, two seconds on, seen = number.
        _room.AddBox<WiredTriggerReceiveSignal>(6, 0, 7, "wf_trg_recv_signal");

        // Placed first, so it runs first.
        if (bGivesItsOwn)
            _room.AddBox<WiredActionGiveVariable>(8, 0, 7, "wf_act_give_var");

        _room.AddBox<WiredActionChangeVariable>(7, 0, 7, "wf_act_change_var_val");
        await ReceiveAsync(6, ANTENNA_B);
        (
            await _room.SaveAsync<UpdateActionMessage>(
                7,
                intParams:
                [
                    (int)WiredVariableTargetType.Global,
                    (int)WiredVariableOperationType.Set,
                    1,
                    0,
                    0,
                    (int)WiredVariableTargetType.Context,
                ],
                definitionSpecifics: [4],
                variableIds: [seenId, _numberId]
            )
        )
            .Should()
            .BeTrue();

        if (bGivesItsOwn)
            await GiveAsync(8, 5);

        await StartAsync(0, 0);
        await StartAsync(0, 4);
        await StartAsync(0, 7);
    }

    /// <summary>Give Variable on the context variable, with "Override existing variable".</summary>
    private async Task GiveAsync(int boxId, int value) =>
        (
            await _room.SaveAsync<UpdateActionMessage>(
                boxId,
                intParams: [(int)WiredVariableTargetType.Context, 0, value, 1],
                definitionSpecifics: [0],
                variableIds: [_numberId]
            )
        )
            .Should()
            .BeTrue();

    private async Task ReceiveAsync(int boxId, int antenna) => (
            await _room.SaveAsync<UpdateTriggerMessage>(
                boxId,
                stuffIds: [antenna],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ]
            )
        ).Should().BeTrue();

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

    private async Task TickAsync(int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            _now += 1_000;
            await Wired.ProcessWiredAsync(_now, dormant: false, Ct);
        }
    }
}
