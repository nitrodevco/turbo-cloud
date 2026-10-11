using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Rooms.Object.Avatars.Player;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// The room setting "let avatars walk through each other", in a one-tile corridor with an avatar
/// standing in the middle. The setting was saved as the client sends it (on means walk through) and
/// read the other way round by the pathfinder, so ticking it stopped avatars passing.
/// </summary>
public sealed class RoomWalkThroughTests
{
    private const int WALKER = 1;
    private static readonly ActionContext Walker = ActionContext.CreateForPlayer(100 + WALKER, 1);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly WiredRoom _room = new(5, 1);
    private readonly RoomPlayerAvatar _walker;

    public RoomWalkThroughTests()
    {
        _walker = _room.Enter(WALKER, 0, 0);
        _room.Enter(2, 2, 0);
    }

    [Fact]
    public async Task With_walk_through_on_an_avatar_walks_past_one_standing_in_its_way()
    {
        AllowWalkThrough(true);

        (await _room.Harness.Room.WalkAvatarToAsync(Walker, 4, 0, Ct)).Should().BeTrue();
        await TickAsync(20);

        (_walker.X, _walker.Y).Should().Be((4, 0));
    }

    [Fact]
    public async Task With_walk_through_off_an_avatar_in_the_way_blocks_the_corridor()
    {
        AllowWalkThrough(false);

        await _room.Harness.Room.WalkAvatarToAsync(Walker, 4, 0, Ct);
        await TickAsync(20);

        _walker.X.Should().BeLessThan(2);
    }

    private void AllowWalkThrough(bool allow)
    {
        var state = _room.Harness.State;
        var snapshot = (RoomSnapshot)RoomHarness.GetMember(state, "RoomSnapshot")!;

        RoomHarness.SetMember(state, "RoomSnapshot", snapshot with { AllowWalkThrough = allow });
    }

    private async Task TickAsync(int ticks)
    {
        for (var tick = 1; tick <= ticks && _walker.IsWalking; tick++)
            await _room.Harness.Room.AvatarTickSystem.ProcessAvatarsAsync(tick * 10_000L, Ct);
    }
}
