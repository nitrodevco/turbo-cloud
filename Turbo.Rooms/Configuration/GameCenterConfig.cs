using Turbo.Primitives.Settings;

namespace Turbo.Rooms.Configuration;

/// <summary>
/// The game centre (<c>docs/plugin-games.md</c>): whether it is open, which game quick join plays,
/// how many games a player may start a day, and the lobby countdown. Read as it is used, so a
/// change in the panel applies at once.
/// </summary>
[LiveSetting]
public sealed class GameCenterConfig
{
    public const string SECTION_NAME = "Turbo:Games";

    /// <summary>Off: the game centre answers "not available" and every join fails.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// The key of the game quick join plays (the client's <c>game.center.default_game</c>); empty
    /// is the registered game with the lowest type id.
    /// </summary>
    public string DefaultGame { get; init; } = "";

    /// <summary>
    /// Games a player may start a day, -1 for no limit. 3 is the client's own default
    /// (<c>Game2GameDirectoryStatusMessageParser</c>) and what a new account is sent.
    /// </summary>
    public int FreeGamesPerDay { get; init; } = 3;

    /// <summary>The same for a Habbo Club member: 30 in the official client's recordings.</summary>
    public int ClubFreeGamesPerDay { get; init; } = 30;

    /// <summary>Seconds a lobby counts down once it has its minimum players.</summary>
    public int CountdownSeconds { get; init; } = 10;

    /// <summary>Most entries one leaderboard request is answered with (the client asks for 50).</summary>
    public int LeaderboardMaxEntries { get; init; } = 50;

    /// <summary>Weeks back a weekly leaderboard can be read.</summary>
    public int LeaderboardMaxWeeksBack { get; init; } = 4;
}
