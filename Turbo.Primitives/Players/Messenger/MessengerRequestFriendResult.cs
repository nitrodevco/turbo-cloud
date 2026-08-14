using Orleans;
using Turbo.Primitives.Players.Enums.Messenger;

namespace Turbo.Primitives.Players.Messenger;

[GenerateSerializer, Immutable]
public record struct MessengerRequestFriendResult(
    bool Success,
    FriendListErrorCodeType? ErrorType = FriendListErrorCodeType.None
);
