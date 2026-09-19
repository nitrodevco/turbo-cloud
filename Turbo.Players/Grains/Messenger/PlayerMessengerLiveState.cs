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
    public Dictionary<PlayerId, MessengerRequestDto> IncomingRequests { get; } = [];
    public List<PlayerId> BlockedPlayerIds { get; } = [];
    public List<PlayerId> IgnoredPlayerIds { get; } = [];
    public Dictionary<int, List<MessageHistoryEntrySnapshot>> Messages { get; } = [];

    /// <summary>Friend list changes waiting for the next flush to the client, one per friend.</summary>
    public Dictionary<PlayerId, MessengerUpdateSnapshot> PendingUpdates { get; } = [];

    /// <summary>Offline messages shown this session, marked delivered by the flush timer.</summary>
    public HashSet<int> PendingDeliveredIds { get; } = [];
    public int NextSessionMessageId { get; set; } = 1;
}
