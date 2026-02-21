using Orleans;
using Turbo.Primitives.Players.Enums.Messenger;

namespace Turbo.Primitives.Players.Messenger;

[GenerateSerializer, Immutable]
public record MessengerAcceptFriendFailure
{
    [Id(0)]
    public required PlayerId SenderId { get; init; }

    [Id(1)]
    public required FriendListErrorCodeType ErrorCode { get; init; }
}
