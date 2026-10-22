using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Game.Directory;

[GenerateSerializer, Immutable]
public sealed record Game2InArenaQueueMessageMessageComposer : IComposer
{
    [Id(0)]
    public required int Position { get; init; }
}
