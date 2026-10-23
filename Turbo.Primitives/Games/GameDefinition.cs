namespace Turbo.Primitives.Games;

/// <summary>
/// A game the game centre offers: what its lobby shows and how many players it takes. The
/// official client knows game type 0 (Snowstorm); a plugin's game takes a type id of its own.
/// </summary>
public sealed record GameDefinition
{
    /// <summary>Unique among the registered games; 0 is the official Snowstorm.</summary>
    public required int GameTypeId { get; init; }

    /// <summary>
    /// A stable name, <c>[a-z0-9_]</c>: what <c>Game2StartSnowWar</c> joins by and what the
    /// client's <c>gamecenter.&lt;key&gt;.*</c> texts are named after.
    /// </summary>
    public required string Key { get; init; }

    /// <summary>The lobby's title (<c>GameLobbyData.levelName</c>).</summary>
    public required string LevelName { get; init; }

    public int FieldType { get; init; }

    /// <summary>Teams the players are dealt into, by join order; 1 is everyone for themselves.</summary>
    public int NumberOfTeams { get; init; } = 1;

    /// <summary>Players a lobby needs before it counts down.</summary>
    public int MinPlayers { get; init; } = 1;

    public int MaxPlayers { get; init; } = 8;

    /// <summary>This game's lobby countdown; null takes the <c>Turbo:Games</c> setting.</summary>
    public int? CountdownSeconds { get; init; }
}
