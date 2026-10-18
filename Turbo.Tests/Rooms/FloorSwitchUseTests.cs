using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Avatars.Player;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// Floor switches through the room's use path, as a double-click reaches it, laid out as Habbo was
/// seen (official client capture, 2026-10-18, build WIN63-202610151745): Floor Switch 1 at (6, 9),
/// Floor Switch 2 at (7, 11). A use from afar walks the avatar to the free tile beside the switch
/// nearest to it (a diagonal one too) and flips the switch on arrival, 6 of 6; a use from beside
/// it flips it at once with no walk and no turn, 4 of 4; Floor Switch 2 steps 0, 1, 2, 3, 0.
/// </summary>
public sealed class FloorSwitchUseTests
{
    private const int SWITCH = 60;
    private const int AVATAR = 1;
    private const int PLAYER = 100 + AVATAR;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly WiredRoom _room = new(12, 14);
    private long _now;

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
        {
            _now += 10_000;
            await _room.Harness.Room.AvatarTickSystem.ProcessAvatarsAsync(_now, Ct);
        }
    }

    // One room tick ahead: far enough for the switch's own tick.
    private Task RunFurniTimersAsync() =>
        _room
            .Harness.Module<RoomTimerSystem>()
            .ProcessTimersAsync(
                (long)(
                    System.Diagnostics.Stopwatch.GetTimestamp()
                    * 1000.0
                    / System.Diagnostics.Stopwatch.Frequency
                ) + 500,
                Ct
            );
}
