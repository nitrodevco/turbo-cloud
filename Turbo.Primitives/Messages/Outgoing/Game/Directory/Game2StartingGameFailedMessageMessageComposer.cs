using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Game.Directory;

[GenerateSerializer, Immutable]
public sealed record Game2StartingGameFailedMessageMessageComposer : IComposer
{
    /// <summary>1 the game was cancelled, 2 not enough players (the parser's constants).</summary>
    [Id(0)]
    public required int Reason { get; init; }
}
