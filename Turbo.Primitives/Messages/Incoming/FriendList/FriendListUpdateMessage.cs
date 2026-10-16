using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.FriendList;

/// <summary>The friend list's update poll, every 1000 s (HabboFriendList.as:413, sendFriendListUpdate).</summary>
public record FriendListUpdateMessage : IClientTimerMessage;
