using Orleans;
using Turbo.Primitives.Games.Snapshots;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Game.Directory;

[GenerateSerializer, Immutable]
public sealed record Game2GameDirectoryStatusMessageMessageComposer : IComposer
{
    [Id(0)]
    public required GameDirectoryStatusSnapshot Status { get; init; }
}
