using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;

namespace Turbo.Primitives.Games;

/// <summary>
/// A game played in a room of the hotel, offered by the game centre. A plugin declares a public,
/// concrete, top-level class implementing it; the server finds it when the plugin loads and
/// removes it when the plugin unloads (<c>docs/plugin-games.md</c>). The game centre runs the
/// lobby, its countdown, the leaderboards and the games-left count; the game runs its room.
/// </summary>
public interface IRoomGame
{
    public GameDefinition Definition { get; }

    /// <summary>
    /// A lobby counted down: put the game in a room and return it. The server then sends every
    /// player <c>Game2GameStarted</c> and forwards them there. Throwing, or a room id of 0 or
    /// less, cancels the game for every player and counts no game.
    /// </summary>
    public Task<GameArena> OpenArenaAsync(GameLaunch launch, CancellationToken ct);

    /// <summary>A player of a started game left it through the game centre.</summary>
    public Task OnPlayerLeftAsync(GameLaunch launch, PlayerId playerId, CancellationToken ct) =>
        Task.CompletedTask;
}

/// <summary>A game that is starting: its id, what it is and who plays it.</summary>
public sealed record GameLaunch
{
    /// <summary>Unique for the server's lifetime; what the result is reported with.</summary>
    public required int GameId { get; init; }

    public required GameDefinition Definition { get; init; }

    public required IReadOnlyList<GameLaunchPlayer> Players { get; init; }
}

/// <summary>A player of a starting game and the team they were dealt.</summary>
public sealed record GameLaunchPlayer(
    PlayerId PlayerId,
    string Name,
    string Figure,
    string Gender,
    int TeamId
);

/// <summary>Where a game is played.</summary>
public sealed record GameArena(RoomId RoomId);
