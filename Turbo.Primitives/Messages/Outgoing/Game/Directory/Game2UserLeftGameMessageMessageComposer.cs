using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Game.Directory;

[GenerateSerializer, Immutable]
public sealed record Game2UserLeftGameMessageMessageComposer : IComposer
{
    // TODO: add properties if/when identified
}
