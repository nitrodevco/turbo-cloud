using System.Text;
using Turbo.Primitives.Orleans.Snapshots.Players;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Object.Logic.Avatars;
using Turbo.Primitives.Rooms.Snapshots.Avatars;

namespace Turbo.Rooms.Object.Avatars.Player;

public sealed class RoomPlayerAvatar
    : RoomAvatar<IRoomPlayer, IRoomPlayerLogic, IRoomPlayerContext>,
        IRoomPlayer
{
    public override RoomObjectType AvatarType { get; } = RoomObjectType.Player;

    public required PlayerId PlayerId { get; init; }
    public AvatarGenderType Gender { get; private set; } = AvatarGenderType.Male;
    public AvatarDanceType DanceType { get; private set; } = AvatarDanceType.None;

    public int GroupId { get; init; } = -1;
    public int GroupStatus { get; init; } = -1;
    public string GroupName { get; init; } = string.Empty;
    public string SwimFigure { get; init; } = string.Empty;
    public int ActivityPoints { get; init; } = 0;
    public bool IsModerator { get; init; } = false;

    public bool UpdateWithPlayer(PlayerSummarySnapshot snapshot)
    {
        Name = snapshot.Name;
        Motto = snapshot.Motto;
        Figure = snapshot.Figure;
        Gender = snapshot.Gender;

        return true;
    }

    public bool SetDance(AvatarDanceType danceType = AvatarDanceType.None)
    {
        if (DanceType == danceType)
            return false;

        if (HasStatus(AvatarStatusType.Sit, AvatarStatusType.Lay))
            return false;

        // check if dance valid
        // check if dance is hc only / validate hc

        DanceType = danceType;

        _snapshot = null;

        return true;
    }

    protected override RoomPlayerAvatarSnapshot BuildSnapshot()
    {
        var statusString = new StringBuilder("/");

        foreach (var (type, value) in Statuses)
            statusString.Append($"{type.ToLegacyString()} {value}/");

        return new()
        {
            AvatarType = AvatarType,
            WebId = PlayerId.Value,
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
            Gender = Gender,
            DanceType = DanceType,
            GroupId = GroupId,
            GroupStatus = GroupStatus,
            GroupName = GroupName,
            SwimFigure = SwimFigure,
            ActivityPoints = ActivityPoints,
            IsModerator = IsModerator,
        };
    }
}
