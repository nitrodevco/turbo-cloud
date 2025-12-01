using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Game.Score;

[GenerateSerializer, Immutable]
public sealed record Game2LeaderboardMessageComposer : IComposer
{
    // TODO: add properties if/when identified
}
