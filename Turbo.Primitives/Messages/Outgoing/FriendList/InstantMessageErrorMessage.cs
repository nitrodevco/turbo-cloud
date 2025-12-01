using Turbo.Primitives.FriendList.Enums;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.FriendList;

public sealed record InstantMessageErrorMessage : IComposer
{
    public required InstantMessageErrorCodeType ErrorCode { get; init; }
    public required long PlayerId { get; init; }
    public required string Message { get; init; }
}
