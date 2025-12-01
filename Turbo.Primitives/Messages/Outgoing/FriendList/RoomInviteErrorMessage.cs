using System.Collections.Generic;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.FriendList;

public sealed record RoomInviteErrorMessage : IComposer
{
    public required int ErrorCode { get; init; }
    public List<int>? FailedRecipients { get; init; }
}
