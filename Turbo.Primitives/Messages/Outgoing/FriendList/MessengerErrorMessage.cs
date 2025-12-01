using Turbo.Contracts.Abstractions;
using Turbo.Primitives.FriendList.Enums;

namespace Turbo.Primitives.Messages.Outgoing.FriendList;

public sealed record MessengerErrorMessage : IComposer
{
    public required int ClientMessageId { get; init; }
    public required FriendListErrorCodeType ErrorCode { get; init; }
}
