using System.Collections.Generic;
using FluentAssertions;
using Turbo.Primitives;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Rooms.Grains.Modules;
using Turbo.Rooms.Object.Avatars.Player;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// A click on the far side of something too tall to step onto. Only the walk judged step
/// height, so the search planned straight over it, the first step was refused and the avatar
/// never moved; it now goes around.
/// </summary>
public sealed class AvatarPathAroundTallStepTests
{
    private const int SIZE = 5;

    [Fact]
    public async Task AnAvatarWalksAroundATileTooHighToStepOnto()
    {
        var heights = new Altitude[SIZE * SIZE];
        heights[2 * SIZE + 2] = Altitude.FromInt(500);
        var harness = new LiveRoomHarness(SIZE, SIZE, heights: heights);
        var avatar = new RoomPlayerAvatar { ObjectId = 5, PlayerId = 9 };
        avatar.SetPosition(1, 2);
        ((IDictionary<RoomObjectId, IRoomAvatar>)
            RoomHarness.GetMember(harness.State, "AvatarsByObjectId")!)[5] = avatar;
        harness.Module<RoomMapModule>().AddAvatar(avatar, false);

        var started = await harness
            .Module<RoomAvatarModule>()
            .WalkAvatarToAsync(avatar, 3, 2, CancellationToken.None);

        for (var tick = 1; tick <= 20 && avatar.IsWalking; tick++)
            await harness.Room.AvatarTickSystem.ProcessAvatarsAsync(
                tick * 10_000L,
                CancellationToken.None
            );

        started.Should().BeTrue();
        (avatar.X, avatar.Y).Should().Be((3, 2));
    }
}
