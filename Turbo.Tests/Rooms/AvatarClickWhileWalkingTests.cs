using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Rooms.Grains.Modules;
using Turbo.Rooms.Object.Avatars.Player;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// A player's click on a tile there is no way to, through the room's walk entry point. In Habbo
/// the avatar keeps walking where it was going; Turbo set the unreachable goal and stopped it
/// (reported with a recording of each). And a walk planned while stepping onto the map's corner
/// tile starts from that tile: tile 0 is a tile, <c>NextTileId</c>'s "none" is -1.
/// </summary>
public sealed class AvatarClickWhileWalkingTests
{
    private const int AVATAR = 5;
    private const int PLAYER = 100 + AVATAR;
    private static readonly ActionContext Player = ActionContext.CreateForPlayer(PLAYER, 1);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly WiredRoom _room = new(10, 10);
    private readonly RoomPlayerAvatar _avatar;

    public AvatarClickWhileWalkingTests()
    {
        _avatar = _room.Enter(AVATAR, 1, 1);

        // Tile C: furni nobody can stand on.
        _room.AddFloorItem(70, 5, 5, canWalk: false);
    }

    private RoomMapModule Map => _room.Map;

    [Fact]
    public async Task A_click_on_furni_while_walking_leaves_the_walk_going_to_where_it_was()
    {
        (await _room.Harness.Room.WalkAvatarToAsync(Player, 8, 1, Ct)).Should().BeTrue();
        await TickAsync(1);
        var goal = _avatar.GoalTileId;

        var refused = await _room.Harness.Room.WalkAvatarToAsync(Player, 5, 5, Ct);

        refused.Should().BeFalse();
        _avatar.IsWalking.Should().BeTrue();
        _avatar.GoalTileId.Should().Be(goal).And.Be(Map.ToIdx(8, 1));

        await TickAsync(20);

        (_avatar.X, _avatar.Y).Should().Be((8, 1));
    }

    [Fact]
    public async Task A_click_on_furni_while_standing_still_leaves_the_avatar_where_it_is()
    {
        var refused = await _room.Harness.Room.WalkAvatarToAsync(Player, 5, 5, Ct);

        refused.Should().BeFalse();
        _avatar.IsWalking.Should().BeFalse();
        _avatar.GoalTileId.Should().Be(-1);
        (_avatar.X, _avatar.Y).Should().Be((1, 1));
    }

    [Fact]
    public async Task A_walk_planned_while_stepping_onto_the_corner_tile_starts_from_it()
    {
        // Mid-step from (1, 1) onto (0, 0), the map's tile 0.
        _avatar.NextTileId = 0;

        (await _room.Harness.Room.WalkAvatarToAsync(Player, 2, 0, Ct)).Should().BeTrue();

        // From (0, 0) the way to (2, 0) is (1, 0) then (2, 0); from (1, 1) it would be one step.
        _avatar.TilePath.Should().Equal(Map.ToIdx(2, 0), Map.ToIdx(1, 0));
    }

    private async Task TickAsync(int ticks)
    {
        for (var tick = 1; tick <= ticks && _avatar.IsWalking; tick++)
            await _room.Harness.Room.AvatarTickSystem.ProcessAvatarsAsync(tick * 10_000L, Ct);
    }
}
