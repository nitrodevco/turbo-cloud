using System;
using System.Collections.Generic;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Messenger;
using Turbo.Primitives.Players.Snapshots.Messenger;

namespace Turbo.Players.Grains.Messenger;

internal sealed class PlayerMessengerLiveState
{
    public required PlayerId PlayerId { get; init; }
    public List<MessengerCategoryDto> Categories { get; } = [];
    public Dictionary<PlayerId, MessengerFriendDto> Friends { get; } = [];

    /// <summary>
    /// The owner's groups as friend list entries (id minus the group id), while the owner is
    /// online and group chats are on. Kept apart from <see cref="Friends"/>: they are no
    /// friendship, count against no limit, and the navigator's friend rooms must not see them.
    /// </summary>
    public Dictionary<PlayerId, MessengerFriendDto> GroupChats { get; } = [];
    public Dictionary<PlayerId, MessengerRequestDto> IncomingRequests { get; } = [];
    public List<PlayerId> BlockedPlayerIds { get; } = [];

    /// <summary>
    /// Ignored players, oldest first. The client drops the first of its own copy when told the
    /// oldest was removed (<c>IgnoredUsersManager.onIgnoreResult</c>), so the order must match.
    /// </summary>
    public List<PlayerId> IgnoredPlayerIds { get; } = [];

    /// <summary>When the friend rows in <see cref="Friends"/> were last read from the database.</summary>
    public DateTime FriendsLoadedAtUtc { get; set; }

    /// <summary>
    /// Whether the <c>Online</c> flag of every friend has been asked of their presence since the
    /// owner came online. Until then every friend reads offline; see
    /// <c>EnsureFriendsOnlineResolvedAsync</c>.
    /// </summary>
    public bool FriendsOnlineResolved { get; set; }

    /// <summary>When the owner last searched for players, for the search rate limit.</summary>
    public DateTime LastSearchAtUtc { get; set; }

    /// <summary>When the owner last sent a room invitation, for the invitation rate limit.</summary>
    public DateTime LastRoomInviteAtUtc { get; set; }

    /// <summary>Friend list changes waiting for the next flush to the client, one per friend.</summary>
    public Dictionary<PlayerId, MessengerUpdateSnapshot> PendingUpdates { get; } = [];

    /// <summary>When the owner's recent console messages were sent, oldest first, for the send rate limit.</summary>
    public Queue<DateTime> RecentMessageTimesUtc { get; } = [];
}
