using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Outgoing.Room.Session;
using Turbo.Primitives.Networking;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// In a room set to leave on the door tile, Habbo (hhus, 2026-10-12) stops a player's walk beside
/// the door and closes their room session (YouArePlayingGame false, CloseConnection) instead of
/// letting them step onto it. Turbo stored the setting and let them stand on the door.
/// </summary>
public sealed class LeaveOnDoorTileTests
{
    private const int AVATAR = 5;
    private const int PLAYER = 100 + AVATAR;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Walking_onto_the_door_leaves_the_room_only_when_the_room_says_so(bool leave)
    {
        var room = new WiredRoom(6, 6);
        var snapshot = RoomHarness.GetMember(room.Harness.State, "RoomSnapshot")!;
        RoomHarness.SetMember(snapshot, "LeaveOnDoorTile", leave);
        var avatar = room.Enter(AVATAR, 2, 0);

        (
            await room.Harness.Room.WalkAvatarToAsync(
                ActionContext.CreateForPlayer(PLAYER, 1),
                0,
                0,
                Ct
            )
        )
            .Should()
            .BeTrue();

        for (var tick = 1; tick <= 10; tick++)
            await room.Harness.Room.AvatarTickSystem.ProcessAvatarsAsync(tick * 10_000L, Ct);

        if (leave)
        {
            room.Harness.Fakes.Log.Of("OnRemovedFromRoomAsync").Should().ContainSingle();
            Sent(room.Harness.Fakes)
                .OfType<YouArePlayingGameMessageComposer>()
                .Should()
                .ContainSingle(composer => !composer.IsPlaying);
            (avatar.X, avatar.Y).Should().NotBe((0, 0), "the walk stops beside the door");
        }
        else
        {
            room.Harness.Fakes.Log.Of("OnRemovedFromRoomAsync").Should().BeEmpty();
            Sent(room.Harness.Fakes).OfType<YouArePlayingGameMessageComposer>().Should().BeEmpty();
            (avatar.X, avatar.Y).Should().Be((0, 0));
        }
    }

    private static IEnumerable<IComposer> Sent(Fakes fakes) =>
        fakes.Log.Calls.SelectMany(call =>
            call.Method is "SendComposerAsync" or "SendComposerToPlayerAsync"
                ? call
                    .Args.OfType<IComposer>()
                    .Concat(call.Args.OfType<IEnumerable<IComposer>>().SelectMany(x => x))
                : []
        );
}
