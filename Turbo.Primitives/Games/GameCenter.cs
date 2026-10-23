namespace Turbo.Primitives.Games;

/// <summary>The values the game centre's messages give a meaning to.</summary>
public static class GameCenter
{
    /// <summary>Games left when there is no limit (the client's <c>hasUnlimitedGames</c>).</summary>
    public const int UNLIMITED = -1;

    /// <summary>The official game, Snowstorm.</summary>
    public const int SNOWWAR_GAME_TYPE = 0;

    /// <summary><c>Game2GameDirectoryStatus</c>: the game centre is open.</summary>
    public const int DIRECTORY_AVAILABLE = 0;

    /// <summary><c>Game2GameDirectoryStatus</c>: closed (the client logs "not available").</summary>
    public const int DIRECTORY_UNAVAILABLE = 1;

    /// <summary><c>Game2JoiningGameFailed</c> reasons, as the client reads <c>reason - 2</c>.</summary>
    public const int JOIN_FAILED_GENERIC = 1;

    public const int JOIN_FAILED_ALREADY_IN_GAME = 6;

    public const int JOIN_FAILED_NO_FREE_GAMES = 8;
}
