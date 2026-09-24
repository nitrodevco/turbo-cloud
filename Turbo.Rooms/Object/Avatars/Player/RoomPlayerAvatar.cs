using System;
using System.Collections.Immutable;
using System.Text;
using Turbo.Primitives.Badges;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Snapshots;
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
    public int GuildStatus { get; private set; } = -1;
    public string GuildName { get; private set; } = string.Empty;
    public string SwimFigure { get; init; } = string.Empty;
    public int ActivityPoints { get; init; } = 0;
    public bool IsModerator { get; init; } = false;

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

    public void SetFavouriteGuild(int guildId, int guildStatus, string guildName)
    {
        GuildId = guildId;
        GuildStatus = guildStatus;
        GuildName = guildName;
    }

    public void SetBadges(ImmutableArray<string> badgeCodes) => BadgeCodes = badgeCodes;

    public void SetHabboClubExpiresAt(DateTime? expiresAt) => HabboClubExpiresAt = expiresAt;

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
            JumpPower = JumpPower,
            Status = statusString.ToString(),
            Gender = Gender,
            DanceType = DanceType,
            EffectId = EffectId,
            IsIdle = IsIdle,
            GroupId = GuildId,
            GroupStatus = GuildStatus,
            GroupName = GuildName,
            SwimFigure = SwimFigure,
            ActivityPoints = ActivityPoints,
            IsModerator = IsModerator,
            BadgesRank = BadgesRank,
        };
    }
}
