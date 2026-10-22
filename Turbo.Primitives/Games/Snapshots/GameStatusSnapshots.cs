using Orleans;

namespace Turbo.Primitives.Games.Snapshots;

/// <summary>What <c>Game2GameDirectoryStatus</c> tells a player about the game centre.</summary>
[GenerateSerializer, Immutable]
public sealed record GameDirectoryStatusSnapshot
{
    /// <summary><see cref="GameCenter.DIRECTORY_AVAILABLE"/> or <see cref="GameCenter.DIRECTORY_UNAVAILABLE"/>.</summary>
    [Id(0)]
    public required int Status { get; init; }

    /// <summary>Seconds the player is blocked from games; 0 when not.</summary>
    [Id(1)]
    public required int BlockLength { get; init; }

    [Id(2)]
    public required int GamesPlayed { get; init; }

    /// <summary><see cref="GameCenter.UNLIMITED"/> for no limit.</summary>
    [Id(3)]
    public required int FreeGamesLeft { get; init; }
}

/// <summary>What <c>Game2AccountGameStatus</c> tells a player about one game.</summary>
[GenerateSerializer, Immutable]
public sealed record GameAccountStatusSnapshot
{
    [Id(0)]
    public required int GameTypeId { get; init; }

    [Id(1)]
    public required int FreeGamesLeft { get; init; }

    [Id(2)]
    public required int GamesPlayedTotal { get; init; }
}
