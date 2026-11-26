using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains.Modules;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Variables;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// User Leaves Room runs its stack for the user who left: the stack reads the variables that
/// user held (tester report 2026-11-26: "any effects/conditions don't receive the correct
/// variable values"). Ours dropped the leaver's values and the avatar before the stack ran, so
/// it triggered for nobody and every variable read as not held. Whether Habbo reads them is
/// taken from the report, not from a capture.
/// </summary>
[Trait("evidence", "inferred")]
public sealed class WiredLeaveRoomVariablesTests
{
    private const int LEAVER = 6;
    private const int LEAVER_PLAYER = 106;
    private const int MARK = 20;

    private readonly WiredRoom _room = new(8, 8);
    private long _now = 10_000;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private RoomWiredSystem Wired => _room.Harness.Module<RoomWiredSystem>();

    public WiredLeaveRoomVariablesTests()
    {
        _room.Enter(5, 1, 1, name: "stays");
        _room.Enter(LEAVER, 2, 1, name: "a1");
    }

    [Theory]
    [InlineData(WiredAvailabilityType.UserActive)]
    [InlineData(WiredAvailabilityType.Persistent)]
    public async Task The_stack_reads_what_the_leaver_held(WiredAvailabilityType availability)
    {
        var mark = await PlaceMarkAsync(availability);
        var key = Key(mark);

        (await mark.GiveValueAsync(key, new WiredVariableValue(7), replace: true))
            .Should()
            .BeTrue();
        await BuildLeaveStackAsync(mark, "L U=$(u) V=$(v)");

        await LeaveAsync();
        await TickAsync(1);

        Wired.GetRoomLogs().Select(x => x.Message).Should().Equal("L U=a1 V=7");
    }

    [Fact]
    public async Task A_value_held_while_in_the_room_is_gone_once_the_stack_has_run()
    {
        var mark = await PlaceMarkAsync(WiredAvailabilityType.UserActive);
        var key = Key(mark);

        (await mark.GiveValueAsync(key, new WiredVariableValue(7), replace: true))
            .Should()
            .BeTrue();
        await BuildLeaveStackAsync(mark, "L V=$(v)");

        await LeaveAsync();
        await TickAsync(1);

        // Nobody is asked for it after that: neither the leaver nor whoever is here.
        mark.TryGetValue(key, out _).Should().BeFalse();
        (await mark.GiveValueAsync(key, new WiredVariableValue(9), replace: true))
            .Should()
            .BeFalse();
    }

    // The door tile, a kick, a ban and an unload take the avatar out through the avatar module,
    // not through the room's leave call; they said nothing, so Leave Room never fired for them.
    [Fact]
    public async Task A_user_taken_out_by_the_room_fires_the_stack_too()
    {
        var mark = await PlaceMarkAsync(WiredAvailabilityType.UserActive);

        (await mark.GiveValueAsync(Key(mark), new WiredVariableValue(7), replace: true))
            .Should()
            .BeTrue();
        await BuildLeaveStackAsync(mark, "L U=$(u) V=$(v)");

        await _room
            .Harness.Module<RoomAvatarModule>()
            .RemoveAvatarFromPlayerAsync(ActionContext.CreateForSystem(1), LEAVER_PLAYER, Ct);
        await TickAsync(1);

        Wired.GetRoomLogs().Select(x => x.Message).Should().Equal("L U=a1 V=7");
    }

    private static WiredVariableKey Key(WiredVariableUser mark) =>
        new(mark.GetVarSnapshot().VariableId, WiredVariableTargetType.User, LEAVER);

    private Task LeaveAsync() =>
        _room.Harness.Room.RemoveAvatarFromPlayerAsync(
            ActionContext.CreateForPlayer(LEAVER_PLAYER, 1),
            LEAVER_PLAYER,
            Ct
        );

    /// <summary>Leave Room writing <paramref name="text"/> to the logs, $(u) the leaver's name and $(v) its mark.</summary>
    private async Task BuildLeaveStackAsync(WiredVariableUser mark, string text)
    {
        _room.AddBox<WiredTriggerHabboLeaveRoom>(1, 0, 0, "wf_trg_leave_room");
        _room.AddBox<WiredActionWriteToLogs>(2, 0, 0, "wf_act_log");
        _room.AddBox<WiredAddonUsernamePlaceholder>(3, 0, 0, "wf_xtra_text_output_username");
        _room.AddBox<WiredAddonVariablePlaceholder>(4, 0, 0, "wf_xtra_text_output_variable");

        (await _room.SaveAsync<UpdateTriggerMessage>(1)).Should().BeTrue();
        (
            await _room.SaveAsync<UpdateAddonMessage>(
                3,
                intParams: [0],
                stringParam: "u",
                playerSources:
                [
                    [WiredPlayerSourceType.TriggeredUser],
                ]
            )
        )
            .Should()
            .BeTrue();
        (
            await _room.SaveAsync<UpdateAddonMessage>(
                4,
                intParams: [0, (int)WiredVariableTargetType.User, 0],
                stringParam: "v",
                variableIds: [mark.GetVarSnapshot().VariableId.ToString()]
            )
        )
            .Should()
            .BeTrue();
        (
            await _room.SaveAsync<UpdateActionMessage>(
                2,
                intParams: [(int)WiredLogLevelType.Warning],
                stringParam: text,
                definitionSpecifics: [0]
            )
        )
            .Should()
            .BeTrue();

        await StackChangedAsync(0, 0);
    }

    private async Task<WiredVariableUser> PlaceMarkAsync(WiredAvailabilityType availability)
    {
        var mark = _room.AddBox<WiredVariableUser>(MARK, 6, 6, "wf_var_user");

        (
            await _room.SaveAsync<UpdateVariableMessage>(
                MARK,
                intParams: [(int)availability, 1],
                stringParam: "mark"
            )
        )
            .Should()
            .BeTrue();
        await mark.LoadWiredAsync(Ct);
        await StackChangedAsync(6, 6);

        return mark;
    }

    private async Task StackChangedAsync(int x, int y)
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
