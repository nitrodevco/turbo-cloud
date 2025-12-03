using System.Collections.Generic;
using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.FriendList;

[GenerateSerializer, Immutable]
public sealed record RoomInviteErrorMessageComposer : IComposer
{
    [Id(0)]
    public required int ErrorCode { get; init; }

    [Id(1)]
    public List<int>? FailedRecipients { get; init; }
}
