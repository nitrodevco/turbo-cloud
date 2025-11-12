using Orleans;
using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Room.Engine;

[GenerateSerializer, Immutable]
public sealed record HeightMapMessageComposer : IComposer
{
    [Id(0)]
    public required int Width { get; init; }

    [Id(1)]
    public required int Size { get; init; }

    [Id(2)]
    public required short[] Heights { get; init; }
}
