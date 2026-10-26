using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Configuration;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Variables;
using Turbo.Rooms.Wired;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// A stack whose effect changes what its own trigger watches runs 100 times in the one burst
/// and stops, with no error logged: the same 100 visits a signal ring gets (official client
/// capture 2026-10-24, WIN63-202610201032). Toggle on the furni "Furni State Is Changed" watches:
/// 101 state updates (the hand's use and 100 toggles) in ~80 ms; "lp + 1" on the variable
/// "Variable Changed" watches, kicked by setting it to 1 and then 1000: 101 and 1100.
/// </summary>
public sealed class WiredRecursionLoopTests
{
    private const int AVATAR = 5;
    private const int LAMP = 30;
    private const int TRIGGER = 1;
    private const int TOGGLE = 2;
    private const int LOG = 3;
    private const int LP = 10;
    private const int CHANGE = 4;

    private readonly WiredRoom _room = new(8, 8);
    private long _now = 10_000;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private RoomWiredSystem Wired => _room.Harness.Module<RoomWiredSystem>();

    public WiredRecursionLoopTests()
    {
        _room.Enter(AVATAR, 1, 1);

        _room.Harness.Room.UseWiredConfig(new WiredConfig { MaxRoomLogEntries = 10_000 });
    }

    // trigger.20.X2: Furni State Is Changed on the lamp + Toggle Furni State on the lamp in one
    // stack, started by one use of the lamp by hand.
    [Fact]
    public async Task Trigger20X2_AStackTogglingTheFurniItsTriggerWatchesRuns100TimesThenStops()
    {
        var lamp = _room.AddFloorItem(LAMP, 5, 5);
        _room.AddBox<WiredTriggerItemStateUpdated>(TRIGGER, 0, 0, "wf_trg_stuff_state");
        _room.AddBox<WiredActionToggleItemState>(TOGGLE, 0, 0, "wf_act_toggle_state");
        await AddLogAsync(0, 0);

        (
            await _room.SaveAsync<UpdateTriggerMessage>(
                TRIGGER,
                intParams: [0],
                stuffIds: [LAMP],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ]
            )
        ).Should().BeTrue();
        (
            await _room.SaveAsync<UpdateActionMessage>(
                TOGGLE,
                intParams: [0],
                stuffIds: [LAMP],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ],
                definitionSpecifics: [0]
            )
        ).Should().BeTrue();
        await StartAsync();

        await _room.Harness.Room.UseItemByIdAsync(_room.ContextFor(AVATAR), LAMP, Ct);
        await TickAsync(1);

        Runs().Should().Be(100);
        // The hand's use and 100 toggles from state 0: an odd number of changes.
        lamp.Logic.GetState().Should().Be(1);

        await TickAsync(4);

        Runs().Should().Be(100, "the loop stops; it does not carry on in later ticks");
        Wired.GetErrorLogs(_now).Should().OnlyContain(x => x.ThrowCount == 0);
    }

    // trigger.22.X2 / action.41.X2: Variable Changed on "lp" + Change Variable "lp + 1" in one
    // stack, kicked from outside wired by setting lp to 1, and later to 1000.
    [Fact]
    public async Task Trigger22X2_AStackChangingTheVariableItsTriggerWatchesRuns100TimesPerKick()
    {
        var lp = _room.AddBox<WiredVariableRoom>(LP, 4, 6, "wf_var_room");
        (
            await _room.SaveAsync<UpdateVariableMessage>(
                LP,
                intParams: [(int)WiredAvailabilityType.RoomActive],
                stringParam: "lp"
            )
        )
            .Should()
            .BeTrue();
        await lp.LoadWiredAsync(Ct);
        await StartAsync(4, 6);

        var lpId = lp.GetVarSnapshot().VariableId;

        _room.AddBox<WiredTriggerVariableChanged>(TRIGGER, 0, 0, "wf_trg_var_changed");
        _room.AddBox<WiredActionChangeVariable>(CHANGE, 0, 0, "wf_act_change_var_val");
        (
            await _room.SaveAsync<UpdateTriggerMessage>(
                TRIGGER,
                intParams: [1, 1, 0, 7, -1, 0],
                variableIds: [lpId.ToString()]
            )
        )
            .Should()
            .BeTrue();
        (
            await _room.SaveAsync<UpdateActionMessage>(
                CHANGE,
                intParams:
                [
                    (int)WiredVariableTargetType.Global,
                    (int)WiredVariableOperationType.Add,
                    0,
                    0,
                    1,
                    (int)WiredVariableTargetType.Global,
                ],
                definitionSpecifics: [0],
                variableIds: [lpId.ToString()]
            )
        )
            .Should()
            .BeTrue();
        await StartAsync();

        var key = new WiredVariableKey(lpId, WiredVariableTargetType.Global, 0);

        async Task<long> KickAsync(long value)
        {
            (
                await lp.SetValueAsync(
                    new WiredExecutionContext(_room.Harness.Room) { CancellationToken = Ct },
                    key,
                    new WiredVariableValue(value)
                )
            )
                .Should()
                .BeTrue();
            await TickAsync(5);
            lp.TryGetValue(key, out var read).Should().BeTrue();

            return read.Value;
        }

        (await KickAsync(1)).Should().Be(101);
        (await KickAsync(1000)).Should().Be(1100);
        Wired.GetErrorLogs(_now).Should().OnlyContain(x => x.ThrowCount == 0);
    }

    private async Task AddLogAsync(int x, int y)
    {
        _room.AddBox<WiredActionWriteToLogs>(LOG, x, y, "wf_act_log");
        (
            await _room.SaveAsync<UpdateActionMessage>(
                LOG,
                intParams: [(int)WiredLogLevelType.Warning],
                stringParam: "run",
                definitionSpecifics: [0]
            )
        )
            .Should()
            .BeTrue();
    }

    private int Runs() => Wired.GetRoomLogPage(1, 10_000, -1, -1, "run").TotalEntries;

    /// <summary>The room builds the stack on (x, y) on the next tick after its box says it changed.</summary>
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

    private async Task TickAsync(int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            _now += 1_000;
            await Wired.ProcessWiredAsync(_now, dormant: false, Ct);
        }
    }
}
