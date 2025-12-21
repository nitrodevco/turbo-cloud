using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Room.Engine;

[GenerateSerializer, Immutable]
public sealed record HeightMapUpdateMessageComposer : IComposer
{
    [Id(0)]
    public required ImmutableArray<(int X, int Y, short Height)> TileHeights { get; init; }
}
