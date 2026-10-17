using Turbo.Primitives.Action;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Rooms.Object.Avatars.Player;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

public class LookToDirectionTests
{
    private static readonly (int X, int Y)[] TargetOffsets =
    [
        (0, -1),
        (1, -1),
        (1, 0),
        (1, 1),
        (0, 1),
        (-1, 1),
        (-1, 0),
        (-1, -1),
    ];

    public static IEnumerable<object[]> Postures()
    {
        foreach (var posture in new[] { "stand", "sit", "lay" })
            for (var target = 0; target < TargetOffsets.Length; target++)
                yield return [posture, (Rotation)target];
    }

    [Theory]
    [MemberData(nameof(Postures))]
    public async Task Look_to_turns_the_head_and_keeps_a_seated_body_still(
        string posture,
        Rotation target
    )
    {
        var harness = new LiveRoomHarness(16, 16);
        var avatar = new RoomPlayerAvatar { ObjectId = 1, PlayerId = 1 };
        avatar.SetPosition(4, 4);
        avatar.SetRotation(Rotation.East);
        if (posture == "sit")
            avatar.Sit(true, rot: Rotation.East);
        if (posture == "lay")
            avatar.Lay(true, rot: Rotation.East);
        (
            (IDictionary<RoomObjectId, IRoomAvatar>)
                RoomHarness.GetMember(harness.State, "AvatarsByObjectId")!
        )[1] = avatar;
        (
            (IDictionary<PlayerId, RoomObjectId>)
                RoomHarness.GetMember(harness.State, "AvatarsByPlayerId")!
        )[1] = 1;
        var offset = TargetOffsets[(int)target];

        Assert.True(
            await harness.Room.LookToAsync(
                ActionContext.CreateForPlayer(1, 1),
                4 + offset.X,
                4 + offset.Y,
                CancellationToken.None
            )
        );

        var inReach =
            target == Rotation.East || target == Rotation.NorthEast || target == Rotation.SouthEast;
        var seated = posture != "stand";
        Assert.Equal(inReach || seated ? Rotation.East : target, avatar.Rotation);
        Assert.Equal(inReach || !seated ? target : Rotation.East, avatar.HeadRotation);
        Assert.Equal(posture == "sit", avatar.HasStatus(AvatarStatusType.Sit));
        Assert.Equal(posture == "lay", avatar.HasStatus(AvatarStatusType.Lay));
    }
}
