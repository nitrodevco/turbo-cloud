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
    public void SetBadges(ImmutableArray<string> badgeCodes);
    public void SetHabboClubExpiresAt(DateTime? expiresAt);
}
