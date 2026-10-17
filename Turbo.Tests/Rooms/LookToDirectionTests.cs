using Turbo.Primitives.Action;
using Turbo.Primitives.Rooms.Enums;
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
        foreach (var posture in new[] { "stand", "floor-sit", "chair", "bed" })
            for (var target = 0; target < TargetOffsets.Length; target++)
                yield return [posture, (Rotation)target];
    }

    [Theory]
    [MemberData(nameof(Postures))]
    public async Task Look_to_turns_the_head_and_keeps_a_body_held_by_a_seat_still(
        string posture,
        Rotation target
    )
    {
        var room = new WiredRoom(16, 16);
        if (posture == "chair")
            room.AddFloorItem(10, 4, 4, canSit: true);
        if (posture == "bed")
            room.AddFloorItem(10, 4, 4, canLay: true);
        var avatar = room.Enter(1, 4, 4);
        avatar.SetRotation(Rotation.East);
        if (posture is "floor-sit" or "chair")
            avatar.Sit(true, rot: Rotation.East);
        if (posture == "bed")
            avatar.Lay(true, rot: Rotation.East);
        var offset = TargetOffsets[(int)target];

        Assert.True(
            await room.Harness.Room.LookToAsync(
                ActionContext.CreateForPlayer(101, 1),
                4 + offset.X,
                4 + offset.Y,
                CancellationToken.None
            )
        );

        // Official client capture: a floor sit turns body and head together like standing; a
        // seat or bed holds the body.
        var inReach =
            target == Rotation.East || target == Rotation.NorthEast || target == Rotation.SouthEast;
        var held = posture is "chair" or "bed";
        Assert.Equal(inReach || held ? Rotation.East : target, avatar.Rotation);
        Assert.Equal(inReach || !held ? target : Rotation.East, avatar.HeadRotation);
        Assert.Equal(posture is "floor-sit" or "chair", avatar.HasStatus(AvatarStatusType.Sit));
        Assert.Equal(posture == "bed", avatar.HasStatus(AvatarStatusType.Lay));
    }
}
