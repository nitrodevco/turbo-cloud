using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.FriendList;

[GenerateSerializer, Immutable]
public sealed record RoomInviteMessageComposer : IComposer
{
    [Id(0)]
    public required int SenderId { get; init; }

    [Id(1)]
    public required string Message { get; init; }
}
