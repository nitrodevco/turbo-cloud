using System.Collections.Immutable;
using Orleans;
using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Room.Engine;

[GenerateSerializer, Immutable]
public sealed record ItemsStateUpdateMessageComposer : IComposer
{
    [Id(0)]
    public required ImmutableDictionary<long, string> ObjectStates { get; init; }
}
