using System.Text;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Snapshots.Avatars;

namespace Turbo.Rooms.Object.Avatars;

internal sealed class RoomPlayerAvatar : RoomAvatar, IRoomPlayerAvatar
{
    public required long PlayerId { get; init; }
    public required AvatarGenderType Gender { get; init; }

    public int GroupId { get; init; } = -1;
    public int GroupStatus { get; init; } = -1;
    public string GroupName { get; init; } = string.Empty;
    public string SwimFigure { get; init; } = string.Empty;
    public int ActivityPoints { get; init; } = 0;
    public bool IsModerator { get; init; } = false;

    protected override RoomPlayerAvatarSnapshot BuildSnapshot()
    {
        var statusString = new StringBuilder("/");

        foreach (var (type, value) in Statuses)
            statusString.Append($"{type.ToLegacyString()} {value}/");

        return new()
        {
            WebId = (int)PlayerId,
            Name = Name,
            Motto = Motto,
            Figure = Figure,
            ObjectId = ObjectId,
            X = X,
            Y = Y,
            Z = Z,
            BodyRotation = Rotation,
            HeadRotation = HeadRotation,
            Status = statusString.ToString(),
            GoalTileId = GoalTileId,
            NextTileId = NextTileId,
            IsWalking = IsWalking,
            Gender = Gender,
            GroupId = GroupId,
            GroupStatus = GroupStatus,
            GroupName = GroupName,
            SwimFigure = SwimFigure,
            ActivityPoints = ActivityPoints,
            IsModerator = IsModerator,
        };
    }
}
