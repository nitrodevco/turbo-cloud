using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms;

namespace Turbo.Primitives.Messages.Outgoing.Navigator;

[GenerateSerializer, Immutable]
public sealed record RoomInfoUpdatedMessageComposer : IComposer
{
    [Id(0)]
    public required RoomId RoomId { get; init; }
}
