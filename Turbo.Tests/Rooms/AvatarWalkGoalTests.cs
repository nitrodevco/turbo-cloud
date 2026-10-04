using FluentAssertions;
using Turbo.Rooms.Object.Avatars.Player;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// A walk's goal and its re-routes. A load test with a thousand bots found walks to busy tiles
/// refused for no reason: every request for a tile counted against the re-route limit of the
/// last walk there, so the third click on a game pad was ignored with the pad free.
/// </summary>
public sealed class AvatarWalkGoalTests
{
    private static RoomPlayerAvatar Avatar() => new() { ObjectId = 1, PlayerId = 1 };

    [Fact]
    public void ANewWalkToTheSameTileIsNeverRefusedAsARepeat()
    {
        var avatar = Avatar();

        for (var i = 0; i < 10; i++)
            avatar.SetGoalTileId(42);

        avatar.GoalTileId.Should().Be(42);
        avatar.TryRerouteGoal().Should().BeTrue();
    }

    [Fact]
    public void AWalkGetsTwoReroutesAndTheThirdStopsIt()
    {
        var avatar = Avatar();
        avatar.SetGoalTileId(42);

        avatar.TryRerouteGoal().Should().BeTrue();
        avatar.TryRerouteGoal().Should().BeTrue();
        avatar.TryRerouteGoal().Should().BeFalse();
        avatar.TryRerouteGoal().Should().BeFalse("the limit holds, not just its third try");
    }

    [Fact]
    public void ANewWalkStartsTheRerouteCountOver()
    {
        var avatar = Avatar();
        avatar.SetGoalTileId(42);
        avatar.TryRerouteGoal();
        avatar.TryRerouteGoal();
        avatar.TryRerouteGoal();

        avatar.SetGoalTileId(42);

        avatar.TryRerouteGoal().Should().BeTrue();
    }

    [Fact]
    public void AnAvatarWithNoGoalHasNothingToReroute()
    {
        var avatar = Avatar();
        avatar.SetGoalTileId(42);

        avatar.SetGoalTileId(-1);

        avatar.GoalTileId.Should().Be(-1);
        avatar.TryRerouteGoal().Should().BeFalse();
    }
}
