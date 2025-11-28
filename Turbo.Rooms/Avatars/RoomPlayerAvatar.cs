using Turbo.Contracts.Enums.Rooms.Object;
using Turbo.Primitives.Rooms.Avatars;
using Turbo.Primitives.Rooms.Snapshots.Avatars;

namespace Turbo.Rooms.Avatars;

internal sealed class RoomPlayerAvatar : RoomAvatar, IRoomPlayerAvatar
{
    public required long PlayerId { get; init; }
    public required AvatarGenderEnum Gender { get; init; }

    public int GroupId { get; init; } = -1;
    public int GroupStatus { get; init; } = -1;
    public string GroupName { get; init; } = string.Empty;
    public string SwimFigure { get; init; } = string.Empty;
    public int ActivityPoints { get; init; } = 0;
    public bool IsModerator { get; init; } = false;

    protected override RoomPlayerAvatarSnapshot BuildSnapshot() =>
        new()
        {
            WebId = (int)PlayerId,
            Name = Name,
            Motto = Motto,
            Figure = Figure,
            ObjectId = ObjectId,
            X = X,
            Y = Y,
            Z = Z,
            Rotation = Rotation,
            Gender = Gender,
            GroupId = GroupId,
            GroupStatus = GroupStatus,
            GroupName = GroupName,
            SwimFigure = SwimFigure,
            ActivityPoints = ActivityPoints,
            IsModerator = IsModerator,
        };
}
