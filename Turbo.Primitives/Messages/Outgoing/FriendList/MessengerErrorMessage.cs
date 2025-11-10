using Turbo.Contracts.Abstractions;
using Turbo.Contracts.Enums.FriendList;

namespace Turbo.Primitives.Messages.Outgoing.FriendList;

public sealed record MessengerErrorMessage : IComposer
{
    public required int ClientMessageId { get; init; }
    public required FriendListErrorCodeEnum ErrorCode { get; init; }
}
