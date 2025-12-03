using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Room.Permissions;

[GenerateSerializer, Immutable]
public sealed record YouAreOwnerMessageComposer : IComposer
{
    [Id(0)]
    public required int RoomId { get; init; }
}
