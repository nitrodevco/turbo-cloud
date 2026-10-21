using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Primitives.Rooms.Snapshots.Avatars;
using Turbo.Rooms.Object.Avatars.Player;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// A head turned by a look-to (clicking someone) looks back to the body after
/// <c>LookToRevertMs</c>, as one turned by chat does; it used to stay turned until the avatar
/// moved. A look-to that turns the whole avatar leaves nothing to look back from.
/// </summary>
public sealed class LookToHeadRevertTests
{
    private const int AVATAR = 1;

    private readonly WiredRoom _room = new(10, 10);
    private readonly RoomPlayerAvatar _avatar;

    public LookToHeadRevertTests()
    {
        _avatar = _room.Enter(AVATAR, 5, 5);
        _avatar.SetRotation(Rotation.East);
    }

    [Fact]
    public async Task A_head_turned_by_a_look_to_looks_back_5_s_later()
    {
        var start = Now();
        var looked = await LookToAsync(6, 4);

        await TickAsync(start + 1_000);
        LastUpdate().Should().Be((Rotation.NorthEast, Rotation.East));
        var before = Updates().Count;

        await TickAsync(looked + 4_999);
        Updates().Should().HaveCount(before);

        await TickAsync(looked + 5_000);

        LastUpdate().Should().Be((Rotation.East, Rotation.East));
        Updates().Should().HaveCount(before + 1);
    }

    [Fact]
    public async Task A_look_to_that_turns_the_whole_avatar_does_not_look_back()
    {
        var start = Now();
        var looked = await LookToAsync(4, 5);

        await TickAsync(start + 1_000);
        LastUpdate().Should().Be((Rotation.West, Rotation.West));
        var before = Updates().Count;

        await TickAsync(looked + 5_000);

        Updates().Should().HaveCount(before);
    }

    /// <summary>The room's clock, which moves only when <see cref="TickAsync"/> moves it.</summary>
    private long Now() => _room.Harness.NowMs();

    private async Task<long> LookToAsync(int x, int y)
    {
        await _room.Harness.Room.LookToAsync(
            ActionContext.CreateForPlayer(100 + AVATAR, _room.Harness.Room.RoomId),
            x,
            y,
            CancellationToken.None
        );

        return Now();
    }

    /// <summary>Moves the room's clock on to <paramref name="at"/> and runs the avatar tick there.</summary>
    private Task TickAsync(long at)
    {
        _room.Clock.AdvanceMs(at - Now());

        return _room.Harness.Room.AvatarTickSystem.ProcessAvatarsAsync(
            Now(),
            CancellationToken.None
        );
    }

    private List<RoomAvatarSnapshot[]> Updates() =>
        [
            .. _room
                .Harness.Fakes.Log.Of("OnNextAsync")
                .Select(c => (RoomOutboundSnapshot)c.Args[0]!)
                .SelectMany(x => x.Composers.OfType<UserUpdateMessageComposer>())
                .Select(x => x.Avatars.ToArray()),
        ];

    private (Rotation Head, Rotation Body) LastUpdate()
    {
        var snapshot = Updates().Last().Should().ContainSingle(a => a.ObjectId == AVATAR).Subject;
        return (snapshot.HeadRotation, snapshot.BodyRotation);
    }
}
