using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Game.Snowwar.Arena;

[GenerateSerializer, Immutable]
public sealed record Game2GameRejoinMessageComposer : IComposer
{
    // TODO: add properties if/when identified
}
