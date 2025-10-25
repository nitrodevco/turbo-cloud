using Turbo.Contracts.Abstractions;
using Turbo.Contracts.Enums.FriendList;

namespace Turbo.Primitives.Messages.Outgoing.FriendList;

public record MessengerErrorMessage : IComposer
{
    public required int ClientMessageId { get; init; }
    public required FriendListErrorCodeEnum ErrorCode { get; init; }
}
