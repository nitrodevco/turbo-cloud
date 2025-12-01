using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Room.Engine;

[GenerateSerializer, Immutable]
public sealed record ItemStateUpdateMessageComposer : IComposer
{
    [Id(0)]
    public required long ObjectId { get; init; }

    [Id(1)]
    public required string State { get; init; }
}
