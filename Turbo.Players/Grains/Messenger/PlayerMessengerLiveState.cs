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
    public Dictionary<int, List<MessageHistoryEntrySnapshot>> Messages = [];
}
