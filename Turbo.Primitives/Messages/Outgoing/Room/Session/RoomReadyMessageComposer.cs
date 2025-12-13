using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Room.Session;

[GenerateSerializer, Immutable]
public sealed record RoomReadyMessageComposer : IComposer
{
    [Id(0)]
    public required string WorldType { get; init; }

    [Id(1)]
    public required int RoomId { get; init; }
}
