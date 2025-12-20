using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;

namespace Turbo.Primitives.Messages.Outgoing.Room.Engine;

[GenerateSerializer, Immutable]
public sealed record ObjectRemoveMultipleMessageComposer : IComposer
{
    [Id(0)]
    public required ImmutableArray<long> ObjectIdsToRemove { get; init; }

    [Id(1)]
    public required PlayerId PickerId { get; init; }
}
