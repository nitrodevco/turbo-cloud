namespace Turbo.Primitives.Rooms.Object.Avatars;

/// <summary>
/// A pet or a bot, the two kinds of avatar that can be told to follow another. They follow the
/// same way and give up the same way, so what that takes is declared once, here.
/// </summary>
public interface IRoomFollower : IRoomAvatar
{
    /// <summary>The avatar being followed, or -1.</summary>
    public RoomObjectId FollowObjectId { get; set; }

    /// <summary>
    /// The tile the followed avatar stood on the last time no way to it was found, or -1. While
    /// the target stays there the follower does not look again until
    /// <see cref="FollowRetryAtMs"/>: an unreachable avatar was searched for on every tick.
    /// </summary>
    public int FollowBlockedTileIdx { get; set; }

    /// <summary>When a follow that found no way looks again although its target has not moved.</summary>
    public long FollowRetryAtMs { get; set; }
}
