using Orleans;
using Turbo.Primitives.Players.Enums.Messenger;

namespace Turbo.Primitives.Players.Messenger;

[GenerateSerializer, Immutable]
public record struct MessengerAddFriendResult(
    bool Success,
    FriendListErrorCodeType? ErrorType = FriendListErrorCodeType.None
);
