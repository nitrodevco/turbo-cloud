using Turbo.Contracts.Abstractions;
using Turbo.Contracts.Enums.FriendList;

namespace Turbo.Primitives.Messages.Outgoing.Friendlist;

public record InstantMessageErrorMessage : IComposer
{
    public required InstantMessageErrorCodeEnum ErrorCode { get; init; }
    public required int PlayerId { get; init; }
    public required string Message { get; init; }
}
