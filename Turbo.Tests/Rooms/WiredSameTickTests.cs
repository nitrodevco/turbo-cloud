using System.Collections.Immutable;
using System.Reflection;
using FluentAssertions;
using Orleans;
using Orleans.Runtime;
using Turbo.Primitives.Achievements.Grains;
using Turbo.Primitives.Achievements.Snapshots;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Grains;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Rooms.Configuration;
using Turbo.Rooms.Grains;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// A wired stack runs in the server tick of what fired it: the lamp a Furni Is Used stack toggles
/// changed 0-1 ms after the floor switch flipped, in one ObjectsDataUpdate with the trigger and
/// the effect, 3 of 3 (official client capture 2026-10-18, WIN63-202610151745); a Says stack's
/// states came in the same ms as the speaker's chat, 3 of 3 (2026-10-14, WIN63-202610131318).
/// Ours queued the event for the next 50 ms wired tick, so the lamp trailed the switch by
/// 43-106 ms. Now a call into the room runs the stacks it fired before it ends, and the room tick
/// runs those its timers fired after them.
/// </summary>
public sealed class WiredSameTickTests
{
    private const int SWITCH = 60;
    private const int LAMP = 61;
    private const int TRIGGER = 62;
    private const int EFFECT = 63;
    private const int ENABLER = 64;
    private const int AVATAR = 1;
    private const int PLAYER = 100 + AVATAR;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly WiredRoom _room = new(12, 14);
    private long _now;
    private long _wiredNow = 10_000;

    private RoomWiredSystem Wired => _room.Harness.Module<RoomWiredSystem>();

    [Fact]
    public async Task A_use_runs_its_stack_before_the_call_into_the_room_ends()
    {
        _room.Enter(AVATAR, 5, 9);
        var floorSwitch = AddSwitch(6, 9);
        var lamp = await BuildFurniIsUsedStackAsync();

        // A packet handler calls the room from outside any grain: the silo's client.
        var sent = await CallAsync(
            UseAsync,
            source: new GrainId(GrainTypePrefix.ClientGrainType, IdSpan.Create("hosted"))
        );

        floorSwitch.Logic.GetState().Should().Be(1);
        lamp.Logic.GetState().Should().Be(1, "the stack ran in the call that flipped the switch");
        sent.OfType<ObjectsDataUpdateMessageComposer>()
            .Should()
            .ContainSingle()
            .Which.StuffDatas.Select(x => x.Item1)
            .Should()
            .BeEquivalentTo(
                new[] { LAMP, TRIGGER, EFFECT }.Select(id => _room.FloorItem(id).ObjectId)
            );
    }

    [Fact]
    public async Task A_switch_its_timer_flips_runs_its_stack_in_the_same_room_tick()
    {
        var avatar = _room.Enter(AVATAR, 3, 9);
        var floorSwitch = AddSwitch(6, 9);
        var lamp = await BuildFurniIsUsedStackAsync();

        await UseAsync();
        for (var tick = 0; tick < 30 && avatar.IsWalking; tick++)
        {
            _now += 10_000;
            await _room.Harness.Room.AvatarTickSystem.ProcessAvatarsAsync(_now, Ct);
        }

        floorSwitch.Logic.GetState().Should().Be(0);

        // The switch looks for the avatar on its furni timer, an avatar tick (500 ms) after the
        // use, and flips for one that has arrived.
        _room.Clock.AdvanceMs(500);
        var sent = await RoomTickAsync();

        floorSwitch.Logic.GetState().Should().Be(1);
        lamp.Logic.GetState().Should().Be(1, "the stack ran in the tick that flipped the switch");
        sent.OfType<ObjectsDataUpdateMessageComposer>()
            .SelectMany(x => x.StuffDatas.Select(s => s.Item1))
            .Should()
            .Contain(lamp.ObjectId);
    }

    [Fact]
    public async Task A_call_that_ends_inside_the_room_tick_leaves_its_stack_to_the_wired_tick()
    {
        _room.Enter(AVATAR, 5, 9);
        AddSwitch(6, 9);
        var lamp = await BuildFurniIsUsedStackAsync();
        _room.Harness.Room.SetTickRunning(true);

        await CallAsync(UseAsync);

        lamp.Logic.GetState().Should().Be(0, "an interleaving call does not run wired mid-tick");

        _room.Harness.Room.SetTickRunning(false);
        await RunWiredAsync();

        lamp.Logic.GetState().Should().Be(1);
    }

    [Fact]
    public async Task A_call_whose_reply_the_caller_sends_on_leaves_its_stack_to_the_wired_tick()
    {
        _room.Enter(AVATAR, 5, 9);
        AddSwitch(6, 9);
        var lamp = await BuildFurniIsUsedStackAsync();

        // An entry: the entering avatar and RoomEntryInfo go out after the call, from its reply.
        await CallAsync(
            UseAsync,
            typeof(IRoomGrain).GetMethod(nameof(IRoomGrain.CreateAvatarFromPlayerAsync))
        );

        lamp.Logic.GetState().Should().Be(0, "the player gets the room before its stacks run");

        await RunWiredAsync();

        lamp.Logic.GetState().Should().Be(1);
    }

    [Fact]
    public async Task A_call_from_a_grain_leaves_its_stack_to_the_tick_so_a_call_back_cannot_hang()
    {
        // The calling grain stands in for any (a presence, a trade, a purchase): it is not
        // reentrant, so it answers the effect's call back only once its own call has returned.
        var callerReturned = new TaskCompletionSource();
        var calledBack = 0;
        _room.Harness.Fakes.Handlers[nameof(IPlayerAchievementGrain.GetAchievementsAsync)] = _ =>
        {
            calledBack++;

#pragma warning disable VSTHRD003 // The grain's answer is held back until the test says its call returned.
            return AnswerOnceReturnedAsync(callerReturned.Task);
#pragma warning restore VSTHRD003
        };
        _room.Enter(AVATAR, 5, 9);
        AddSwitch(6, 9);
        await BuildProgressAchievementStackAsync();

        try
        {
            var call = CallAsync(
                UseAsync,
                source: GrainId.Create("playerachievement", PLAYER.ToString())
            );

            (await Task.WhenAny(call, Task.Delay(5_000, Ct)))
                .Should()
                .BeSameAs(call, "the room does not wait on the grain waiting on it");
            calledBack.Should().Be(0);
        }
        finally
        {
            callerReturned.TrySetResult();
        }

        await RunWiredAsync();

        calledBack.Should().Be(1, "the tick runs the stack, outside the caller's call");
    }

    [Fact]
    public async Task Events_past_the_ticks_budget_wait_for_the_next_wired_tick()
    {
        _room.Harness.Room.UseWiredConfig(new WiredConfig { MaxEventsPerTick = 1 });
        _room.Enter(AVATAR, 5, 9);
        AddSwitch(6, 9);
        var lamp = await BuildFurniIsUsedStackAsync();

        await UseThroughCallAsync();
        lamp.Logic.GetState().Should().Be(1);

        await UseThroughCallAsync();
        lamp.Logic.GetState().Should().Be(1, "the tick's one event is spent");

        _wiredNow += 50;
        await Wired.ProcessWiredAsync(_wiredNow, dormant: false, Ct);

        lamp.Logic.GetState().Should().Be(0);
    }

    [Fact]
    public async Task Nothing_runs_before_the_rooms_first_wired_tick()
    {
        _room.Enter(AVATAR, 5, 9);
        AddSwitch(6, 9);
        var lamp = await BuildFurniIsUsedStackAsync(initialize: false);

        await UseThroughCallAsync();

        lamp.Logic.GetState().Should().Be(0, "Room Initialized fires before anything queued");

        await RunWiredAsync();

        lamp.Logic.GetState().Should().Be(1);
    }

    private Task UseThroughCallAsync() => CallAsync(UseAsync);

    private Task UseAsync() =>
        _room.Harness.Room.UseItemByIdAsync(ActionContext.CreateForPlayer(PLAYER, 1), SWITCH, Ct);

    /// <summary>A call into the room as Orleans makes it: through the room's call filter.</summary>
    private async Task<List<IComposer>> CallAsync(
        Func<Task> call,
        MethodInfo? method = null,
        GrainId? source = null
    )
    {
        var filter = (object)_room.Harness.Room as IIncomingGrainCallFilter;
        filter.Should().NotBeNull("the room runs what a call fired before the call ends");

        _room.Harness.Fakes.Handlers["Invoke"] = c =>
            c.Interface == typeof(IIncomingGrainCallContext) ? call() : Fakes.NotHandled;
        _room.Harness.Fakes.Handlers["get_InterfaceMethod"] = c =>
            c.Interface == typeof(IIncomingGrainCallContext) ? method : Fakes.NotHandled;
        _room.Harness.Fakes.Handlers["get_SourceId"] = c =>
            c.Interface == typeof(IIncomingGrainCallContext) ? source : Fakes.NotHandled;

        var before = _room.Harness.Fakes.SentToRoom<IComposer>().Count();
        await filter!.Invoke(_room.Harness.Fakes.Create<IIncomingGrainCallContext>());

        return [.. _room.Harness.Fakes.SentToRoom<IComposer>().Skip(before)];
    }

    private async Task<List<IComposer>> RoomTickAsync()
    {
        var before = _room.Harness.Fakes.SentToRoom<IComposer>().Count();
        var tick = typeof(RoomGrain).GetMethod(
            "ProcessRoomTickAsync",
            BindingFlags.Instance | BindingFlags.NonPublic
        )!;
        await (Task)tick.Invoke(_room.Harness.Room, [Ct])!;

        return [.. _room.Harness.Fakes.SentToRoom<IComposer>().Skip(before)];
    }

    private async Task<IRoomFloorItem> BuildFurniIsUsedStackAsync(bool initialize = true)
    {
        var lamp = _room.AddFloorItem(LAMP, 8, 9, canWalk: false);
        _room.AddBox<WiredTriggerHabboChangesItemState>(TRIGGER, 0, 0, "wf_trg_state_changed");
        _room.AddBox<WiredActionToggleItemState>(EFFECT, 0, 0, "wf_act_toggle_state");
        (
            await _room.SaveAsync<UpdateTriggerMessage>(
                TRIGGER,
                stuffIds: [SWITCH],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ]
            )
        ).Should().BeTrue();
        (
            await _room.SaveAsync<UpdateActionMessage>(
                EFFECT,
                intParams: [0],
                stuffIds: [LAMP],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ],
                definitionSpecifics: [0]
            )
        ).Should().BeTrue();
        await Wired.OnRoomEventAsync(
            new RoomWiredStackChangedEvent
            {
                RoomId = 1,
                CausedBy = ActionContext.CreateForSystem(1),
                StackIds = [_room.Map.ToIdx(0, 0)],
            },
            Ct
        );

        if (initialize)
            await RunWiredAsync();

        return lamp;
    }

    private static async Task<ImmutableArray<AchievementSnapshot>> AnswerOnceReturnedAsync(
        Task callerReturned
    )
    {
#pragma warning disable VSTHRD003 // The test completes this once the caller's call has returned.
        await callerReturned;
#pragma warning restore VSTHRD003

        return [];
    }

    /// <summary>Furni Is Used on the switch -> Progress Achievement for the user who used it.</summary>
    private async Task BuildProgressAchievementStackAsync()
    {
        _room.AddBox<WiredAddonAchievementEnabler>(ENABLER, 10, 1, "wf_xtra_achievement_enabler");
        (await _room.SaveAsync<UpdateAddonMessage>(ENABLER, stringParam: "Lap")).Should().BeTrue();
        await RebuildAsync(10, 1);
        _room.AddBox<WiredTriggerHabboChangesItemState>(TRIGGER, 0, 0, "wf_trg_state_changed");
        (
            await _room.SaveAsync<UpdateTriggerMessage>(
                TRIGGER,
                stuffIds: [SWITCH],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ]
            )
        ).Should().BeTrue();
        _room.AddBox<WiredActionProgressAchievement>(EFFECT, 0, 0, "wf_act_progress_ach");
        (
            await _room.SaveAsync<UpdateActionMessage>(
                EFFECT,
                intParams: [1, 0, 5, 1],
                stringParam: "Lap"
            )
        )
            .Should()
            .BeTrue();
        await RebuildAsync(0, 0);
    }

    private async Task RebuildAsync(int x, int y)
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
        await RunWiredAsync();
    }

    private async Task RunWiredAsync()
    {
        for (var i = 0; i < 10; i++)
        {
            _wiredNow += 50;
            await Wired.ProcessWiredAsync(_wiredNow, dormant: false, Ct);
        }
    }

    private IRoomFloorItem AddSwitch(int x, int y) =>
        _room.AddFloorItem(
            SWITCH,
            x,
            y,
            "wf_floor_switch1",
            canWalk: false,
            createLogic: (_, ctx) =>
                _room.Harness.LogicProvider.CreateLogicInstance("wf_floor_switch1", ctx),
            totalStates: 2
        );
}
