using System.Collections.Generic;
using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players.Enums.Messenger;

namespace Turbo.Primitives.Messages.Outgoing.FriendList;

[GenerateSerializer, Immutable]
public sealed record RoomInviteErrorMessageComposer : IComposer
{
    [Id(0)]
    public required RoomInviteErrorCodeType ErrorCode { get; init; }

    /// <summary>Written only for <see cref="RoomInviteErrorCodeType.RecipientsFailed"/>.</summary>
    [Id(1)]
    public List<int>? FailedRecipients { get; init; }
}
