using System.Collections.Immutable;
using Orleans;
using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Room.Engine;

[GenerateSerializer, Immutable]
public sealed record SlideObjectBundleMessageComposer : IComposer
{
    [Id(0)]
    public required int OldX { get; init; }

    [Id(1)]
    public required int OldY { get; init; }

    [Id(2)]
    public required int NewX { get; init; }

    [Id(3)]
    public required int NewY { get; init; }

    [Id(4)]
    public required int RollerItemId { get; init; }

    [Id(5)]
    public required ImmutableArray<(long, string, string)> FloorHeights { get; init; }
}
