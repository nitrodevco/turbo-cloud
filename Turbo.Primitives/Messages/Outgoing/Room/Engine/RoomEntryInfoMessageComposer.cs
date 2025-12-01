using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Room.Engine;

[GenerateSerializer, Immutable]
public sealed record RoomEntryInfoMessageComposer : IComposer
{
    [Id(0)]
    public required long RoomId { get; init; }

    [Id(1)]
    public required bool IsOwner { get; init; }
}
