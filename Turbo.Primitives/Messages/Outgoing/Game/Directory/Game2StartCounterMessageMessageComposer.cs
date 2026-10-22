using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Game.Directory;

[GenerateSerializer, Immutable]
public sealed record Game2StartCounterMessageMessageComposer : IComposer
{
    /// <summary>Seconds until the game starts.</summary>
    [Id(0)]
    public required int CountDownLength { get; init; }
}
