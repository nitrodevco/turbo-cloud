using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Room.Engine;

[GenerateSerializer, Immutable]
public sealed record RoomPropertyMessageComposer : IComposer
{
    [Id(0)]
    public required string Key { get; init; }

    [Id(1)]
    public required string Value { get; init; }
}
