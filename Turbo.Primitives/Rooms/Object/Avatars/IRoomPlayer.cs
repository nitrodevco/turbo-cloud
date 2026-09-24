using System;
using System.Collections.Immutable;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Snapshots;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object.Logic.Avatars;
using Turbo.Primitives.Rooms.Snapshots;

namespace Turbo.Primitives.Rooms.Object.Avatars;

public interface IRoomPlayer : IRoomAvatar<IRoomPlayer, IRoomPlayerLogic, IRoomPlayerContext>
{
    new IRoomPlayerLogic Logic { get; }
    public PlayerId PlayerId { get; }
    public AvatarGenderType Gender { get; }
    public int AchievementScore { get; }

    /// <summary>How this player came to be in the room; it does not change while they are here.</summary>
    public RoomEntrySnapshot RoomEntry { get; }
    public void SetRoomEntry(RoomEntrySnapshot entry);

    /// <summary>Codes of the badges the player wears, loaded when the avatar enters.</summary>
    public ImmutableArray<string> BadgeCodes { get; }

    /// <summary>
    /// When this player's Habbo Club membership runs out; null when they hold none. It is the
    /// moment rather than a flag on purpose: a membership that expires while the player stands
    /// in the room then needs nobody to notice and nothing to be pushed, because whoever reads
    /// it compares it against the time they read it.
    /// </summary>
    public DateTime? HabboClubExpiresAt { get; }
    public bool UpdateWithPlayer(PlayerSummarySnapshot snapshot);

    /// <summary>
    /// The group whose badge this player wears, or <c>-1</c>. Wired reads it, the info stand
    /// draws it, and the room sends it on with every avatar; it is on the avatar rather than
    /// fetched per use because both of those are hot paths.
    /// </summary>
    public int GuildId { get; }

    /// <summary>The wearer's standing in that group, as the client numbers membership.</summary>
    public int GuildStatus { get; }

    public string GuildName { get; }

    public void SetFavouriteGuild(int guildId, int guildStatus, string guildName);

    public void SetBadges(ImmutableArray<string> badgeCodes);
    public void SetHabboClubExpiresAt(DateTime? expiresAt);
}
