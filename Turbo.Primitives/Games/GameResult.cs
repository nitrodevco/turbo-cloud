using System.Collections.Generic;
using Turbo.Primitives.Players;

namespace Turbo.Primitives.Games;

/// <summary>How a game ended, reported once by the game through <see cref="IGameResultService"/>.</summary>
public sealed record GameResult
{
    public required int GameTypeId { get; init; }

    /// <summary>The <see cref="GameLaunch.GameId"/>; a game reported twice is stored once.</summary>
    public required int GameId { get; init; }

    public required IReadOnlyList<GamePlayerResult> Players { get; init; }
}

/// <summary>A player's score in a game, their team and where they finished (1 is first).</summary>
public sealed record GamePlayerResult(PlayerId PlayerId, int Score, int TeamId, int Placement);
