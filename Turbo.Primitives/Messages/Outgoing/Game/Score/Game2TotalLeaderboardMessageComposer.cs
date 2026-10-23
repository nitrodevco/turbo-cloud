using Orleans;
using Turbo.Primitives.Games.Snapshots;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Game.Score;

[GenerateSerializer, Immutable]
public sealed record Game2TotalLeaderboardMessageComposer : IComposer
{
    [Id(0)]
    public required GameLeaderboardSnapshot Leaderboard { get; init; }
}
