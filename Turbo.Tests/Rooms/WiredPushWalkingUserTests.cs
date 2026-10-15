using FluentAssertions;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object;
using Turbo.Rooms.Object.Avatars.Player;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Wired;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// Move/Rotate User on a walking user (official client capture, 2026-10-14, a 0.5 s repeater
/// pushing east): the push replaces the step in progress, from the tile being stepped onto, and
/// is sent as a walk (move type 0) facing the way the user walks; the walk goes on to its goal
/// from there. Pushed back onto the tile it came from it walks into the push again ("treadmill");
/// pushed on its last step the walk ends where the push left it. This server stopped the walk.
/// </summary>
public sealed class WiredPushWalkingUserTests
{
    private const int AVATAR = 5;
    private const int MOVE_USER = 2;

    private readonly WiredRoom _room = new(10, 10);
    private readonly RoomPlayerAvatar _avatar;
    private readonly WiredActionMoveRotateUser _push;
    private long _now;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public WiredPushWalkingUserTests()
    {
        _avatar = _room.Enter(AVATAR, 5, 6);
        _push = _room.AddBox<WiredActionMoveRotateUser>(MOVE_USER, 0, 0, "wf_act_move_rotate_user");
    }

    [Fact]
    public async Task Pushed_back_onto_the_tile_it_left_it_keeps_walking_into_the_push()
    {
        await SaveAsync(Rotation.East);
        await WalkToAsync(1, 6);
        await TickAsync(1);
        _avatar.NextTileId.Should().Be(_room.Map.ToIdx(4, 6));

        var ctx = await PushAsync();

        (_avatar.X, _avatar.Y).Should().Be((5, 6));
        _avatar.IsWalking.Should().BeTrue();
        _avatar.GoalTileId.Should().Be(_room.Map.ToIdx(1, 6));

        var move = ctx.UserMoves.Should().ContainSingle().Subject;

        (move.SourceX, move.SourceY, move.TargetX, move.TargetY).Should().Be((5, 6, 5, 6));
        move.MoveType.Should().Be(SlideAvatarMoveType.Move);
        move.BodyDirection.Should().Be(Rotation.West);

        await TickAsync(1);

        _avatar.NextTileId.Should().Be(_room.Map.ToIdx(4, 6), "it steps west again");
    }

    [Fact]
    public async Task Pushed_aside_it_walks_on_to_its_goal()
    {
        await SaveAsync(Rotation.North);
        await WalkToAsync(1, 6);
        await TickAsync(1);

        await PushAsync();

        (_avatar.X, _avatar.Y).Should().Be((4, 5));
        _avatar.IsWalking.Should().BeTrue();

        await TickAsync(10);

        (_avatar.X, _avatar.Y).Should().Be((1, 6));
    }

    [Fact]
    public async Task Pushed_on_its_last_step_the_walk_ends_there()
    {
        await SaveAsync(Rotation.North);
        await WalkToAsync(4, 6);
        await TickAsync(1);

        await PushAsync();

        await TickAsync(5);

        (_avatar.X, _avatar.Y).Should().Be((4, 5));
        _avatar.IsWalking.Should().BeFalse();
    }

    [Fact]
    public async Task A_standing_user_still_slides()
    {
        await SaveAsync(Rotation.East);

        var ctx = await PushAsync();

        (_avatar.X, _avatar.Y).Should().Be((6, 6));
        ctx.UserMoves.Should()
            .ContainSingle()
            .Which.MoveType.Should()
            .Be(SlideAvatarMoveType.Slide);
    }

    private async Task SaveAsync(Rotation direction) => (
            await _room.SaveAsync<UpdateActionMessage>(
                MOVE_USER,
                intParams: [(int)direction, -1],
                playerSources:
                [
                    [WiredPlayerSourceType.TriggeredUser],
                ],
                definitionSpecifics: [0]
            )
        ).Should().BeTrue();

    private async Task WalkToAsync(int x, int y) =>
        (await _room.Harness.Room.AvatarModule.WalkAvatarToAsync(_avatar, x, y, Ct))
            .Should()
            .BeTrue();

    private async Task<WiredExecutionContext> PushAsync()
    {
        var ctx = new WiredExecutionContext(_room.Harness.Room) { CancellationToken = Ct };

        ctx.Selected.SelectedAvatarIds.Add((RoomObjectId)AVATAR);

        await _push.ExecuteAsync(ctx, Ct);

        return ctx;
    }

    private async Task TickAsync(int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            _now += 10_000;
            await _room.Harness.Room.AvatarTickSystem.ProcessAvatarsAsync(_now, Ct);
        }
    }
}
