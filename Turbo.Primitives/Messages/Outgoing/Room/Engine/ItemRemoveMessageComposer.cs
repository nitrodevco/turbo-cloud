using Orleans;
using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Room.Engine;

[GenerateSerializer, Immutable]
public sealed record ItemRemoveMessageComposer : IComposer
{
    [Id(0)]
    public required long ObjectId { get; init; }

    [Id(1)]
    public required long PickerId { get; init; }
}
