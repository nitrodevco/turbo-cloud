using Turbo.Contracts.Abstractions;
using Turbo.Contracts.Enums.FriendList;

namespace Turbo.Primitives.Messages.Outgoing.FriendList;

public sealed record InstantMessageErrorMessage : IComposer
{
    public required InstantMessageErrorCodeEnum ErrorCode { get; init; }
    public required long PlayerId { get; init; }
    public required string Message { get; init; }
}
