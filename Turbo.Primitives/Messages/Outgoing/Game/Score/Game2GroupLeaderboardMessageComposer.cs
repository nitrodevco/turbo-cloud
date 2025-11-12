using Orleans;
using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Game.Score;

[GenerateSerializer, Immutable]
public sealed record Game2GroupLeaderboardMessageComposer : IComposer
{
    // TODO: add properties if/when identified
}
