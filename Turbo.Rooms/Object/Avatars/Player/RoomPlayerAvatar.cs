using System;
using System.Collections.Immutable;
using Turbo.Primitives.Badges;
using Turbo.Primitives.Guilds.Enums;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Players.Snapshots;
using Turbo.Primitives.Players.Snapshots.Permissions;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Object.Logic.Avatars;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Primitives.Rooms.Snapshots.Avatars;

namespace Turbo.Rooms.Object.Avatars.Player;

public sealed class RoomPlayerAvatar
    : RoomAvatar<IRoomPlayer, IRoomPlayerLogic, IRoomPlayerContext>,
        IRoomPlayer
{
    public override RoomObjectType AvatarType { get; } = RoomObjectType.Player;

    public required PlayerId PlayerId { get; init; }
    public AvatarGenderType Gender { get; private set; } = AvatarGenderType.Male;
    public ImmutableArray<string> BadgeCodes { get; private set; } = [];
    public DateTime? HabboClubExpiresAt { get; private set; }

    /// <summary>The player's place on the total badges board, as their summary last said.</summary>
    public int BadgesRank { get; private set; } = BadgeRanks.NONE;
    public int AchievementScore { get; private set; }
    public RoomEntrySnapshot RoomEntry { get; private set; } = RoomEntrySnapshot.Default;

    public void SetRoomEntry(RoomEntrySnapshot entry) => RoomEntry = entry;

    public int GuildId { get; private set; } = -1;
    public GuildMembershipStatus GuildStatus { get; private set; } = GuildMembershipStatus.None;
    public string GuildName { get; private set; } = string.Empty;
    public string SwimFigure { get; init; } = string.Empty;
    public ResolvedPermissionsSnapshot Permissions { get; private set; } =
        ResolvedPermissionsSnapshot.EMPTY;

    public bool IsModerator => Permissions.Has(PermissionNodes.Room.MODERATE_ANY);

    public bool UpdateWithPlayer(PlayerSummarySnapshot snapshot)
    {
        Name = snapshot.Name;
        Motto = snapshot.Motto;
        Figure = snapshot.Figure;
        Gender = snapshot.Gender;
        BadgesRank = snapshot.BadgesRank;
        AchievementScore = snapshot.AchievementScore;

        return true;
    }

    public void SetFavouriteGuild(int guildId, GuildMembershipStatus guildStatus, string guildName)
    {
        GuildId = guildId;
        GuildStatus = guildStatus;
        GuildName = guildName;
    }

    public void SetPermissions(ResolvedPermissionsSnapshot permissions)
    {
        var wasModerator = IsModerator;

        Permissions = permissions;

        // The flag travels in the avatar snapshot, so a change has to reach the room.
        if (IsModerator != wasModerator)
            MarkDirty();
    }

    public void SetBadges(ImmutableArray<string> badgeCodes) => BadgeCodes = badgeCodes;

    public void SetHabboClubExpiresAt(DateTime? expiresAt) => HabboClubExpiresAt = expiresAt;

    protected override RoomPlayerAvatarSnapshot BuildSnapshot()
    {
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
            JumpPower = JumpPower,
            Status = BuildStatusString(),
            Gender = Gender,
            DanceType = DanceType,
            EffectId = EffectId,
            IsIdle = IsIdle,
            GroupId = GuildId,
            GroupStatus = GuildStatus,
            GroupName = GuildName,
            SwimFigure = SwimFigure,
            // `RoomUserData.activityPoints` is the achievement score: the infostand's score row.
            ActivityPoints = AchievementScore,
            IsModerator = IsModerator,
            BadgesRank = BadgesRank,
        };
    }
}
