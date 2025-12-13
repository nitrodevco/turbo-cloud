using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Room.Session;

[GenerateSerializer, Immutable]
public sealed record OpenConnectionMessageComposer : IComposer
{
    [Id(0)]
    public required int RoomId { get; init; }
}
