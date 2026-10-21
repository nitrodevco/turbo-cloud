using FluentAssertions;
using Turbo.Primitives.Achievements;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Rooms.Configuration;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Avatars.Player;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// Floor switches through the room's use path, as a double-click reaches it, laid out as Habbo was
/// seen (official client capture, 2026-10-18, build WIN63-202610151745): Floor Switch 1 at (6, 9),
/// Floor Switch 2 at (7, 11). A use from afar walks the avatar to the free tile beside the switch
/// nearest to it (a diagonal one too) and flips the switch on arrival, 6 of 6; a use from beside
/// it flips it at once with no walk and no turn, 4 of 4; Floor Switch 2 steps 0, 1, 2, 3, 0.
/// A "Furni Is Used" trigger picking the switch fires with the flip, not at the use request: the
/// lamp its Toggle Furni State effect points at changed in the switch's own update tick, 3 of 3,
/// and not at all when the avatar walked off before arriving, 2 of 2; the flip came ~300 ms after
/// the last step update (official client capture, 2026-10-18).
/// </summary>
public sealed class FloorSwitchUseTests
{
    private const int SWITCH = 60;
    private const int LAMP = 61;
    private const int TRIGGER = 62;
    private const int EFFECT = 63;
    private const int AVATAR = 1;
    private const int PLAYER = 100 + AVATAR;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly WiredRoom _room;
    private long _now;
    private long _wiredNow = 10_000;

    public FloorSwitchUseTests() => _room = new(12, 14);

    private FloorSwitchUseTests(RoomConfig roomConfig) => _room = new(12, 14, roomConfig);

    [Theory]
    [InlineData(3, 9, 5, 9)]
    [InlineData(6, 6, 6, 8)]
    [InlineData(9, 7, 7, 8)]
    public async Task A_use_from_afar_walks_to_the_nearest_tile_beside_it_and_flips_on_arrival(
        int fromX,
        int fromY,
        int toX,
        int toY
    )
    {
        var avatar = _room.Enter(AVATAR, fromX, fromY);
        var floorSwitch = AddSwitch("wf_floor_switch1", 6, 9);

        await UseAsync();

        floorSwitch.Logic.GetState().Should().Be(0, "it does not flip from afar");
        _room.Map.GetTileXY(avatar.GoalTileId).Should().Be((toX, toY));

        await WalkUntilStoppedAsync(avatar);
        await RunFurniTimersAsync();

        (avatar.X, avatar.Y).Should().Be((toX, toY));
        floorSwitch.Logic.GetState().Should().Be(1);
    }

    [Fact]
    public async Task A_use_from_beside_it_flips_it_at_once_without_walking_or_turning()
    {
        var avatar = _room.Enter(AVATAR, 7, 8);
        avatar.SetBodyRotation(Rotation.West);
        var floorSwitch = AddSwitch("wf_floor_switch1", 6, 9);

        await UseAsync();

        floorSwitch.Logic.GetState().Should().Be(1);
        avatar.IsWalking.Should().BeFalse();
        (avatar.X, avatar.Y).Should().Be((7, 8));
        avatar.Rotation.Should().Be(Rotation.West, "the avatar does not turn to face it");
    }

    [Fact]
    public async Task Floor_switch_2_used_from_afar_steps_through_its_four_states()
    {
        var avatar = _room.Enter(AVATAR, 7, 8);
        var floorSwitch = AddSwitch("wf_floor_switch2", 7, 11, totalStates: 4);

        await UseAsync();
        await WalkUntilStoppedAsync(avatar);
        await RunFurniTimersAsync();

        (avatar.X, avatar.Y).Should().Be((7, 10));
        floorSwitch.Logic.GetState().Should().Be(1);

        List<int> states = [];
        for (var i = 0; i < 4; i++)
        {
            await UseAsync();
            states.Add(floorSwitch.Logic.GetState());
        }

        states.Should().Equal(2, 3, 0, 1);
    }

    [Fact]
    public async Task Walking_off_somewhere_else_on_the_way_does_not_flip_it()
    {
        var avatar = _room.Enter(AVATAR, 1, 1);
        var floorSwitch = AddSwitch("wf_floor_switch1", 6, 9);

        await UseAsync();
        await _room.Harness.Room.WalkAvatarToAsync(
            ActionContext.CreateForPlayer(PLAYER, 1),
            1,
            12,
            Ct
        );
        await WalkUntilStoppedAsync(avatar);
        await RunFurniTimersAsync();

        floorSwitch.Logic.GetState().Should().Be(0);
    }

    [Fact]
    public async Task Furni_is_used_fires_when_the_switch_flips_on_arrival_not_at_the_use()
    {
        var avatar = _room.Enter(AVATAR, 3, 9);
        AddSwitch("wf_floor_switch1", 6, 9);
        var lamp = await BuildFurniIsUsedStackAsync();

        await UseAsync();
        await RunWiredAsync();

        lamp.Logic.GetState().Should().Be(0, "nothing happens at the use request");

        await WalkUntilStoppedAsync(avatar);
        await RunFurniTimersAsync();
        await RunWiredAsync();

        _room.FloorItem(SWITCH).Logic.GetState().Should().Be(1);
        lamp.Logic.GetState().Should().Be(1, "the trigger fires with the flip");
    }

    [Fact]
    public async Task Furni_is_used_does_not_fire_when_the_avatar_walks_off_before_arriving()
    {
        var avatar = _room.Enter(AVATAR, 3, 9);
        AddSwitch("wf_floor_switch1", 6, 9);
        var lamp = await BuildFurniIsUsedStackAsync();

        await UseAsync();
        await StepAsync();
        await _room.Harness.Room.WalkAvatarToAsync(
            ActionContext.CreateForPlayer(PLAYER, 1),
            3,
            9,
            Ct
        );
        await WalkUntilStoppedAsync(avatar);
        await RunFurniTimersAsync();
        await RunWiredAsync();

        _room.FloorItem(SWITCH).Logic.GetState().Should().Be(0);
        lamp.Logic.GetState().Should().Be(0);
    }

    [Fact]
    public async Task Furni_is_used_fires_at_once_for_a_use_from_beside_it()
    {
        _room.Enter(AVATAR, 5, 9);
        AddSwitch("wf_floor_switch1", 6, 9);
        var lamp = await BuildFurniIsUsedStackAsync();

        await UseAsync();
        await RunWiredAsync();

        lamp.Logic.GetState().Should().Be(1);
    }

    [Fact]
    public async Task The_furniture_toggle_achievement_counts_the_flip_on_arrival()
    {
        var avatar = _room.Enter(AVATAR, 3, 9);
        AddSwitch("wf_floor_switch1", 6, 9);

        await UseAsync();

        ToggleFacts().Should().Be(0);

        await WalkUntilStoppedAsync(avatar);
        await RunFurniTimersAsync();

        ToggleFacts().Should().Be(1);
    }

    [Fact]
    public async Task It_flips_the_configured_time_after_the_last_step_before_the_avatar_stops()
    {
        var test = new FloorSwitchUseTests(new RoomConfig { FloorSwitchUseAfterLastStepMs = 0 });
        var avatar = test._room.Enter(AVATAR, 3, 9);
        var floorSwitch = test.AddSwitch("wf_floor_switch1", 6, 9);

        await test.UseAsync();
        await test.StepUntilLastStepAsync(avatar);

        avatar.IsWalking.Should().BeTrue("the avatar is still taking its last step");
        floorSwitch.Logic.GetState().Should().Be(1);
    }

    [Fact]
    public async Task By_default_it_flips_300_ms_after_the_last_step()
    {
        var avatar = _room.Enter(AVATAR, 3, 9);
        var floorSwitch = AddSwitch("wf_floor_switch1", 6, 9);

        await UseAsync();
        await StepUntilLastStepAsync(avatar);

        floorSwitch.Logic.GetState().Should().Be(0, "not in the tick of the last step itself");

        _room.Clock.AdvanceMs(299);
        await RunFurniTimersAsync();

        floorSwitch.Logic.GetState().Should().Be(0, "a millisecond short of 300 ms");

        _room.Clock.AdvanceMs(1);
        await RunFurniTimersAsync();

        avatar.IsWalking.Should().BeTrue();
        floorSwitch.Logic.GetState().Should().Be(1);
    }

    private async Task StepUntilLastStepAsync(RoomPlayerAvatar avatar)
    {
        await RunFurniTimersAsync();

        for (var tick = 0; tick < 10 && avatar.TilePath.Count > 0; tick++)
        {
            await StepAsync();
            await RunFurniTimersAsync();
        }

        avatar.TilePath.Should().BeEmpty();
        _room.Map.GetTileXY(avatar.NextTileId).Should().Be((5, 9));
    }

    private async Task<IRoomFloorItem> BuildFurniIsUsedStackAsync()
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
        await RunWiredAsync();

        return lamp;
    }

    private RoomWiredSystem Wired => _room.Harness.Module<RoomWiredSystem>();

    private async Task RunWiredAsync()
    {
        for (var i = 0; i < 10; i++)
        {
            _wiredNow += 50;
            await Wired.ProcessWiredAsync(_wiredNow, dormant: false, Ct);
        }
    }

    private int ToggleFacts() =>
        _room
            .Harness.Fakes.Log.Of("Record")
            .Count(x => ((AchievementFact)x.Args[2]!).Source == AchievementSources.FURNITURE);

    private Task UseAsync() =>
        _room.Harness.Room.UseItemByIdAsync(ActionContext.CreateForPlayer(PLAYER, 1), SWITCH, Ct);

    private IRoomFloorItem AddSwitch(string name, int x, int y, int totalStates = 2) =>
        _room.AddFloorItem(
            SWITCH,
            x,
            y,
            name,
            canWalk: false,
            createLogic: (_, ctx) => _room.Harness.LogicProvider.CreateLogicInstance(name, ctx),
            totalStates: totalStates
        );

    private async Task WalkUntilStoppedAsync(RoomPlayerAvatar avatar)
    {
        for (var tick = 0; tick < 30 && avatar.IsWalking; tick++)
            await StepAsync();
    }

    private Task StepAsync()
    {
        _now += 10_000;

        return _room.Harness.Room.AvatarTickSystem.ProcessAvatarsAsync(_now, Ct);
    }

    // One room tick ahead: far enough for the switch's own tick.
    private Task RunFurniTimersAsync() =>
        _room.Harness.Module<RoomTimerSystem>().ProcessTimersAsync(_room.Harness.NowMs() + 500, Ct);
}
