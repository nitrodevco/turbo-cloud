using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Avatar;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Configuration;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Variables;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// What wired keeps when a room nobody is in unloads and is loaded again, as Habbo does it
/// (official client capture 2026-10-24, WIN63-202610201032: the owner went to the hotel view for
/// 49 s, the room unloaded, and they came back in). Every case here goes through a real unload:
/// the room grain deactivates, hands its changes to persistence, and a new one reads the furni
/// back. Variables: a room variable available while the room is active is still there at 0, a
/// permanent one keeps its value; a furni variable available while the room is active is gone
/// from the furni, a permanent one kept; a user variable available while the user is in the
/// room is gone, a permanent one kept (variable.2.R1, variable.0.R1, variable.1.R1). Timers: the
/// periodic triggers all fire on the room's first tick with the user in it, about 470 ms after
/// RoomReady, then each on its period (trigger.6.R4, trigger.12.R4, trigger.19.R4); "at given
/// time" fires once, its time after that first tick (trigger.3.R4). Event triggers stay armed
/// (trigger.18.R4, trigger.16.R4).
/// </summary>
public sealed class WiredRoomUnloadTests
{
    private const int TRIGGER = 1;
    private const int EFFECT = 2;
    private const int PAD = 3;
    private const int LAMP = 4;
    private const int BLOCK = 5;
    private const int ROOM_ACTIVE = 10;
    private const int ROOM_PERMANENT = 11;
    private const int FURNI_ACTIVE = 12;
    private const int FURNI_PERMANENT = 13;
    private const int USER_ACTIVE = 14;
    private const int USER_PERMANENT = 15;

    // The owner's avatar is given a new room index on every entry; the player stays the same.
    private const int OWNER_PLAYER = 101;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static ActionContext Owner => ActionContext.CreateForSystem(1);

    private RoomLoads _loads = new();

    private WiredRoom Room => _loads.Room!;

    private RoomWiredSystem Wired => Room.Harness.Module<RoomWiredSystem>();

    // trigger.6.R4 (periodically [4], 2 s), trigger.12.R4 (long [1], 5 s), trigger.19.R4
    // (short [10], 500 ms): the reload's first flash came ~470 ms after RoomReady, in the user's
    // first UserUpdate, for all three in the same tick (2/2 each).
    [Theory]
    [InlineData("wf_trg_periodically", 4, 2_000)]
    [InlineData("wf_trg_period_long", 1, 5_000)]
    [InlineData("wf_trg_period_short", 10, 500)]
    public async Task A_reloaded_room_fires_its_periodic_trigger_on_the_first_tick_then_each_period(
        string logic,
        int units,
        int periodMs
    )
    {
        await BuildTimerStackAsync(logic, units);
        await EnterAsync(1);
        var before = await RunAsync(periodMs * 3 + 100);

        before.Zip(before.Skip(1), (a, b) => b - a).Should().AllBeEquivalentTo(periodMs);

        await UnloadForAsync(49_000);
        await RunAsync(300);

        var entered = await EnterAsync(2, offBoundaryMs: 120);
        var after = await RunAsync(periodMs * 2 + 600);

        after[0].Should().Be(FirstAvatarTick(entered), "the first tick with the user in it fires");
        after[1].Should().Be(after[0] + periodMs);
    }

    // A box placed in a room already loaded is no load: its first firing waits a period.
    [Fact]
    public async Task A_periodic_trigger_placed_in_a_loaded_room_waits_a_period()
    {
        _loads.AddFloorItem(PAD, 3, 3, "default_floor");
        await _loads.LoadAsync(Ct);
        var entered = await EnterAsync(1, offBoundaryMs: 120);
        await RunAsync(1_000);

        Room.AddBox<Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers.WiredTriggerPeriodically>(
            TRIGGER,
            0,
            0,
            "wf_trg_periodically"
        );
        Room.AddBox<Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions.WiredActionToggleItemState>(
            EFFECT,
            0,
            0,
            "wf_act_toggle_state"
        );
        await SaveTimerStackAsync(4);
        await Wired.OnRoomEventAsync(
            new RoomWiredStackChangedEvent
            {
                RoomId = 1,
                CausedBy = Owner,
                StackIds = [Room.Map.ToIdx(0, 0)],
            },
            Ct
        );
        var placedAt = Room.Harness.NowMs();
        var fired = await RunAsync(2_600);

        fired[0].Should().BeGreaterThanOrEqualTo(placedAt + 2_000);
        entered.Should().BeLessThan(placedAt);
    }

    // trigger.3.R4: at_given_time [4] (2 s) fired once 2.0 s after the reload's first flash tick
    // (72 770 -> 74 770 ms), and not again (1/1). trigger.3.R5: re-saved after it fired, it does
    // not fire again (2/2).
    [Fact]
    public async Task At_given_time_fires_once_after_every_load_and_not_after_a_save()
    {
        await BuildTimerStackAsync("wf_trg_at_given_time", 4);
        var firstEntry = await EnterAsync(1);
        var before = await RunAsync(6_000);

        before.Should().ContainSingle();
        before[0].Should().BeInRange(firstEntry + 2_000, firstEntry + 2_100);

        await SaveTimerStackAsync(4);
        (await RunAsync(3_000)).Should().BeEmpty("a save does not start it again");

        await UnloadForAsync(49_000);
        await RunAsync(300);
        var entered = await EnterAsync(2);
        var after = await RunAsync(6_000);

        after.Should().ContainSingle("every load starts it again");
        after[0].Should().BeInRange(entered + 2_000, entered + 2_100);
    }

    // trigger.3.R5 (wlab4): a NEW at_given_time placed in a room loaded 72 min earlier and saved
    // [2] (1 s) never fired, within 5 s of the placement nor 4.5 s of the save (2/2 each); it
    // fired once ~1.45 s after the next load's RoomReady (4/4).
    [Fact]
    public async Task A_new_at_given_time_past_its_time_waits_for_the_next_load()
    {
        _loads.AddFloorItem(EFFECT, 0, 0, "wf_act_toggle_state");
        _loads.AddFloorItem(PAD, 3, 3, "default_floor");
        await _loads.LoadAsync(Ct);
        await EnterAsync(1);
        await RunAsync(10_000);

        Room.AddBox<WiredTriggerAtTime>(TRIGGER, 0, 0, "wf_trg_at_given_time");
        _loads.AddFloorItem(TRIGGER, 0, 0, "wf_trg_at_given_time");
        await RunAsync(100);
        await SaveTimerStackAsync(2);

        (await RunAsync(5_000)).Should().BeEmpty("the room timer passed 1 s long ago");

        await UnloadForAsync(49_000);
        await RunAsync(300);
        // The test furni rows keep no wired data over the reload: the room's copy is saved again.
        await SaveTimerStackAsync(2);
        var entered = await EnterAsync(2);
        var after = await RunAsync(4_000);

        after.Should().ContainSingle("the next load starts the timer again");
        after[0].Should().BeInRange(entered + 1_000, entered + 1_100);
    }

    // trigger.18.R4: Click Furni on the lamp fires before and after the reload (2/2);
    // trigger.16.R4: User Performs Action wave fires 90 ms after the wave, both times (2/2).
    [Fact]
    public async Task Event_triggers_are_still_armed_after_a_reload()
    {
        _loads.AddFloorItem(TRIGGER, 0, 0, "wf_trg_click_furni");
        _loads.AddFloorItem(EFFECT, 0, 0, "wf_act_toggle_state");
        _loads.AddFloorItem(LAMP, 4, 4, "default_floor");
        _loads.AddFloorItem(PAD, 3, 3, "default_floor");
        _loads.AddFloorItem(TRIGGER + 20, 1, 0, "wf_trg_user_performs_action");
        _loads.AddFloorItem(EFFECT + 20, 1, 0, "wf_act_toggle_state");
        _loads.AddFloorItem(PAD + 20, 5, 5, "default_floor");
        await _loads.LoadAsync(Ct);
        await RunAsync(100);
        (
            await Room.SaveAsync<UpdateTriggerMessage>(
                TRIGGER,
                stuffIds: [LAMP],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ]
            )
        ).Should().BeTrue();
        await SaveToggleAsync(EFFECT, PAD);
        (
            await Room.SaveAsync<UpdateTriggerMessage>(
                TRIGGER + 20,
                intParams: [(int)WiredAvatarActionType.Wave]
            )
        )
            .Should()
            .BeTrue();
        await SaveToggleAsync(EFFECT + 20, PAD + 20);

        for (var load = 0; load < 2; load++)
        {
            if (load > 0)
                await UnloadForAsync(49_000);

            var index = 1 + load;

            await EnterAsync(index);
            await RunAsync(600);
            var pad = Room.FloorItem(PAD).Logic.GetState();
            var wavePad = Room.FloorItem(PAD + 20).Logic.GetState();

            await Room.FloorItem(LAMP).Logic.OnClickAsync(Room.ContextFor(index), 0, Ct);
            await Wired.OnRoomEventAsync(
                new AvatarPerformsActionEvent
                {
                    RoomId = 1,
                    CausedBy = Room.ContextFor(index),
                    ObjectId = index,
                    ActionType = Turbo.Primitives.Rooms.Enums.AvatarActionType.Expression,
                    Value = (int)Turbo.Primitives.Rooms.Enums.AvatarExpressionType.Wave,
                },
                Ct
            );
            await RunAsync(200);

            Room.FloorItem(PAD)
                .Logic.GetState()
                .Should()
                .NotBe(pad, $"the click fires on load {load + 1}");
            Room.FloorItem(PAD + 20)
                .Logic.GetState()
                .Should()
                .NotBe(wavePad, $"the wave fires on load {load + 1}");
        }
    }

    // variable.2.R1 (room ra [1] = 5, rp [10] = 7), variable.0.R1 (furni fa [1,1] = 3, fp [1,10]
    // = 4 on one block), variable.1.R1 (user ua [0,1] = 8, up [10,1] = 9): after the reload ra
    // reads 0 and is still listed, rp 7; fa is not listed on the block at all, fp 4; ua is not
    // listed on the user, up 9 (1/1 each).
    [Fact]
    public async Task Variables_after_a_reload_keep_only_what_is_permanent()
    {
        _loads.AddFloorItem(ROOM_ACTIVE, 1, 1, "wf_var_room");
        _loads.AddFloorItem(ROOM_PERMANENT, 1, 2, "wf_var_room");
        _loads.AddFloorItem(FURNI_ACTIVE, 2, 1, "wf_var_furni");
        _loads.AddFloorItem(FURNI_PERMANENT, 2, 2, "wf_var_furni");
        _loads.AddFloorItem(USER_ACTIVE, 3, 1, "wf_var_user");
        _loads.AddFloorItem(USER_PERMANENT, 3, 2, "wf_var_user");
        _loads.AddFloorItem(BLOCK, 5, 5, "default_floor");
        await _loads.LoadAsync(Ct);
        await RunAsync(100);

        var ra = await SaveVariableAsync(ROOM_ACTIVE, "ra", (int)WiredAvailabilityType.RoomActive);
        var rp = await SaveVariableAsync(
            ROOM_PERMANENT,
            "rp",
            (int)WiredAvailabilityType.Persistent
        );
        var fa = await SaveVariableAsync(
            FURNI_ACTIVE,
            "fa",
            1,
            (int)WiredAvailabilityType.RoomActive
        );
        var fp = await SaveVariableAsync(
            FURNI_PERMANENT,
            "fp",
            1,
            (int)WiredAvailabilityType.Persistent
        );
        var ua = await SaveVariableAsync(
            USER_ACTIVE,
            "ua",
            (int)WiredAvailabilityType.UserActive,
            1
        );
        var up = await SaveVariableAsync(
            USER_PERMANENT,
            "up",
            (int)WiredAvailabilityType.Persistent,
            1
        );
        await EnterAsync(1);
        await RunAsync(100);

        await SetAsync(Global(), ra, WiredVariableMenuOperationType.SetValue, 5);
        await SetAsync(Global(), rp, WiredVariableMenuOperationType.SetValue, 7);
        await SetAsync(Furni(), fa, WiredVariableMenuOperationType.Create, 3);
        await SetAsync(Furni(), fp, WiredVariableMenuOperationType.Create, 4);
        await SetAsync(User(1), ua, WiredVariableMenuOperationType.Create, 8);
        await SetAsync(User(1), up, WiredVariableMenuOperationType.Create, 9);

        (await ValuesAsync(Global())).Should().Contain(new[] { (ra, 5), (rp, 7) });
        (await ValuesAsync(Furni())).Should().Contain(new[] { (fa, 3), (fp, 4) });
        (await ValuesAsync(User(1))).Should().Contain(new[] { (ua, 8), (up, 9) });

        await UnloadForAsync(49_000);
        await EnterAsync(2);
        await RunAsync(100);

        var room = await ValuesAsync(Global());
        room.Should().Contain(new[] { (ra, 0), (rp, 7) });
        var furni = await ValuesAsync(Furni());
        furni.Should().Contain((fp, 4));
        furni.Select(x => x.Id).Should().NotContain(fa);
        var user = await ValuesAsync(User(2));
        user.Should().Contain((up, 9));
        user.Select(x => x.Id).Should().NotContain(ua);
    }

    private static WiredVariableBinding Global() => new(WiredVariableTargetType.Global, 0);

    private static WiredVariableBinding Furni() => new(WiredVariableTargetType.Furni, BLOCK);

    private static WiredVariableBinding User(int roomIndex) =>
        new(WiredVariableTargetType.User, roomIndex);

    private async Task<WiredVariableId> SaveVariableAsync(
        int boxId,
        string name,
        params int[] intParams
    )
    {
        (
            await Room.SaveAsync<UpdateVariableMessage>(
                boxId,
                intParams: intParams,
                stringParam: name
            )
        )
            .Should()
            .BeTrue();
        await RunAsync(100);

        return ((FurnitureWiredVariableLogic)Room.FloorItem(boxId).Logic)
            .GetVarSnapshot()
            .VariableId;
    }

    private async Task SetAsync(
        WiredVariableBinding binding,
        WiredVariableId variable,
        WiredVariableMenuOperationType operation,
        int value
    ) =>
        (
            await Room.Harness.Room.ApplyWiredVariableMenuOperationAsync(
                Owner,
                binding,
                variable,
                operation,
                new WiredVariableValue(value),
                Ct
            )
        )
            .Should()
            .BeTrue();

    private async Task<List<(WiredVariableId Id, int Value)>> ValuesAsync(
        WiredVariableBinding binding
    ) =>
        [
            .. (
                await Room.Harness.Room.GetAllVariablesForBindingAsync(Owner, binding, Ct)
            )!.Values.Select(x => (x.id, x.value.ToClient())),
        ];

    // A timer of type `logic` on 0,0 toggling PAD, saved by the owner in the loaded room.
    private async Task BuildTimerStackAsync(string logic, int units)
    {
        _loads.AddFloorItem(TRIGGER, 0, 0, logic);
        _loads.AddFloorItem(EFFECT, 0, 0, "wf_act_toggle_state");
        _loads.AddFloorItem(PAD, 3, 3, "default_floor");
        await _loads.LoadAsync(Ct);
        await RunAsync(100);
        await SaveTimerStackAsync(units);
    }

    private async Task SaveTimerStackAsync(int units)
    {
        (await Room.SaveAsync<UpdateTriggerMessage>(TRIGGER, intParams: [units])).Should().BeTrue();
        await SaveToggleAsync(EFFECT, PAD);
    }

    private async Task SaveToggleAsync(int effectId, int padId) => (
            await Room.SaveAsync<UpdateActionMessage>(
                effectId,
                intParams: [0],
                stuffIds: [padId],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ],
                definitionSpecifics: [0]
            )
        ).Should().BeTrue();

    /// <summary>
    /// The owner leaves, the empty room unloads, and they come back <paramref name="awayMs"/>
    /// later: the room loads again (not yet entered).
    /// </summary>
    private async Task UnloadForAsync(int awayMs)
    {
        foreach (var objectId in Room.Avatars.Keys.ToList())
        {
            Room.Harness.State.AvatarsByObjectId.Remove(objectId);
            Room.Harness.State.AvatarsByPlayerId.Remove(OWNER_PLAYER);
        }

        await _loads.UnloadAsync(Ct);
        _loads.Clock.AdvanceMs(awayMs);
        await _loads.LoadAsync(Ct);
    }

    /// <summary>
    /// The owner walks in as room index <paramref name="roomIndex"/>, <paramref name="offBoundaryMs"/>
    /// past an avatar tick; when they are in.
    /// </summary>
    private async Task<long> EnterAsync(int roomIndex, int offBoundaryMs = 0)
    {
        var avatarTickMs = new RoomConfig().AvatarTickMs;
        var now = Room.Harness.NowMs();
        var target = now - now % avatarTickMs + avatarTickMs + offBoundaryMs;

        await RunAsync(target - now);
        Room.Enter(roomIndex, 6, 6, playerId: OWNER_PLAYER);

        return Room.Harness.NowMs();
    }

    private static long FirstAvatarTick(long entered)
    {
        var avatarTickMs = new RoomConfig().AvatarTickMs;

        return entered % avatarTickMs == 0
            ? entered
            : entered - entered % avatarTickMs + avatarTickMs;
    }

    /// <summary>
    /// Wired ticks every 50 ms for <paramref name="forMs"/>, as the room runs them (dormant while
    /// nobody is in); when PAD toggled.
    /// </summary>
    private async Task<List<long>> RunAsync(long forMs)
    {
        var fired = new List<long>();
        var until = Room.Harness.NowMs() + forMs;

        while (Room.Harness.NowMs() < until)
        {
            var pad = Room.Harness.State.ItemsById.TryGetValue((RoomObjectId)PAD, out var item)
                ? item.Logic.GetState()
                : 0;

            _loads.Clock.AdvanceMs(50);
            var now = Room.Harness.NowMs();
            await Wired.ProcessWiredAsync(
                now,
                dormant: Room.Harness.State.AvatarsByPlayerId.Count == 0,
                Ct
            );

            if (item is not null && item.Logic.GetState() != pad)
                fired.Add(now);
        }

        return fired;
    }
}
