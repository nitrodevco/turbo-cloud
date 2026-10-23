using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Game.Directory;

[GenerateSerializer, Immutable]
public sealed record Game2UserBlockedMessageMessageComposer : IComposer
{
    /// <summary>Seconds the player is blocked from games.</summary>
    [Id(0)]
    public required int PlayerBlockLength { get; init; }
}
