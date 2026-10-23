using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;

namespace Turbo.Primitives.Messages.Outgoing.Game.Directory;

[GenerateSerializer, Immutable]
public sealed record Game2UserLeftGameMessageMessageComposer : IComposer
{
    [Id(0)]
    public required PlayerId PlayerId { get; init; }
}
