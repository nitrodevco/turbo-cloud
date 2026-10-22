# Game centre and plugin room games

The game centre is Habbo's games window (the toolbar's games icon, the friend bar's game
notices): a player sees how many games they have left, quick-joins a game, waits in its lobby
while it fills and counts down, plays, and reads the game's leaderboards. Turbo Cloud runs the
game centre's protocol (the `Game2*` directory and score messages) and lets a plugin add its own
game to it: a game played in a room of the hotel, with the lobby, countdown, leaderboards and
rewards handled by the server.

The official client knows one game, Snowstorm (game type `0`, `game.center.default_game=snowwar`),
and its arena is not part of Turbo Cloud. A hotel whose plugins register no game has a closed game
centre: the directory answers "not available" and the client shows nothing.

## The protocol, as the server answers it

| Client sends | Server answers |
| --- | --- |
| `Game2CheckGameDirectoryStatus` (toolbar games icon, friend bar game notice) | `Game2GameDirectoryStatus(status, blockLength, gamesPlayed, freeGamesLeft)`: status `0` when the game centre is on and its default game is registered, `1` otherwise. `freeGamesLeft` `-1` is unlimited. |
| `Game2GetAccountGameStatus(gameTypeId)` (on the subscription status, after a game) | `Game2AccountGameStatus(gameTypeId, freeGamesLeft, gamesPlayedTotal)`. |
| `Game2QuickJoinGame` (the play button) | Joins the default game's open lobby, or opens one: `Game2GameCreated(lobby)` to the joiner, `Game2UserJoinedGame(player, false)` to the others. When the lobby has its minimum players, `Game2StartCounter(seconds)`; a player leaving below the minimum, `Game2StopCounter`. At the end of the count, `Game2GameStarted(lobby)` and the game's arena (below). A refusal is `Game2JoiningGameFailed(reason)`: `8` no free games left, `6` already in a game, `1` anything else. |
| `Game2StartSnowWar(key)` | As quick join, for the game whose `Key` is `key`. |
| `Game2LeaveGame` (the lobby's close) | `Game2UserLeftGame(userId)` to the lobby; the last one out cancels it. |
| `Game2GetTotalLeaderboard`, `Game2GetWeeklyLeaderboard`, `Game2GetFriendsLeaderboard`, `Game2GetWeeklyFriendsLeaderboard`, `Game2GetTotalGroupLeaderboard`, `Game2GetWeeklyGroupLeaderboard` | The matching `Game2*Leaderboard` event: entries `(userId, score, rank, name, figure, gender)`, the list size and the game type; weekly ones add `year, week, maxOffset, currentOffset, minutesUntilReset`; group ones rank groups and add the player's favourite group. |

The lobby (`GameLobbyData`) is `gameId, levelName, gameType, fieldType, numberOfTeams,
maximumPlayers, owningPlayerName, levelEntryId` and its players `(userId, name, figure, gender,
teamId, skillLevel, totalScore, scoreToNextLevel)`.

## Registering a room game

Declare a public, concrete, top-level class implementing `IRoomGame` (`Turbo.Primitives.Games`) in
the plugin assembly. When the plugin loads, Turbo Cloud builds it with the plugin's services and
registers it in `IGameRegistry`; when the plugin unloads or hot-reloads it is removed (open lobbies
of that game are cancelled) and disposed if it is `IDisposable`.

```csharp
public sealed class CollectGame(IGrainFactory grains) : IRoomGame
{
    public GameDefinition Definition { get; } =
        new()
        {
            GameTypeId = 100,        // unique; 0 is the official Snowstorm
            Key = "collect",         // texts: gamecenter.collect.name, ...
            LevelName = "Collect",   // the lobby's title
            NumberOfTeams = 2,
            MinPlayers = 2,
            MaxPlayers = 8,
        };

    // The lobby counted down: put the game in a room and say which. The server then sends every
    // player Game2GameStarted and forwards them to that room.
    public async Task<GameArena> OpenArenaAsync(GameLaunch launch, CancellationToken ct)
    {
        var roomId = await PickFreeArenaAsync(ct);
        // ... set teams, reset the room's game, start the stage countdown ...
        return new GameArena(roomId);
    }
}
```

| Member | |
| --- | --- |
| `GameDefinition` | `GameTypeId` (int, unique, `> 0` for plugins), `Key` (stable, `[a-z0-9_]`, the client's `gamecenter.<key>.*` texts), `LevelName`, `FieldType` (0), `NumberOfTeams` (1 = free for all), `MinPlayers`, `MaxPlayers`. Optional per-game overrides of the settings below: `CountdownSeconds`, `FreeGamesPerDay`. |
| `OpenArenaAsync(GameLaunch, ct)` | Called once per lobby at the end of its countdown. `GameLaunch` has `GameId`, the `Definition` and `Players` (`PlayerId`, `Name`, `Figure`, `Gender`, `TeamId`; teams are dealt round-robin by join order). Return the room to send the players to. Throwing (or returning `RoomId <= 0`) cancels the game: `Game2StartingGameFailed` to every player, no game is counted. |
| `OnPlayerLeftAsync(GameLaunch, PlayerId, ct)` | Optional (default does nothing): a player of a started game left it through the game centre. |

## In the arena room

The arena is an ordinary room, so the game uses what rooms already have: the room's game system
(`RoomGameSystem`: teams, team scores, `StartGameAsync` / `EndGameAsync`, the `GameStartedEvent`,
`GameScoreChangedEvent`, `GameEndedEvent` room events), its timers and scoreboards (the game timer,
counter and scoreboard furni and their wired), and `IRoomEventListener` (see
`plugin-room-events.md`) to hear players walk, click and leave. A stage countdown is a room game
timer started by the plugin; nothing in the game centre waits on it.

## Reporting a result

When a game ends, report every player's score once:

```csharp
await services.GetRequiredService<IGameResultService>().ReportAsync(
    new GameResult
    {
        GameTypeId = 100,
        GameId = launch.GameId,
        Players = [new GamePlayerResult(playerId, Score: 120, TeamId: 1, Placement: 1), ...],
    },
    ct
);
```

The server stores the scores (the total and weekly leaderboards, the player's games played), then
calls every registered `IGameRewardHandler` with the result. A score of 0 still counts the game. A
`GameId` reported twice is stored once.

## Rewards

Rewards are a plugin's: declare a public class implementing `IGameRewardHandler`
(`Task OnGameResultAsync(GameResult result, CancellationToken ct)`), found and registered as games
are. It runs after the scores are saved; grant credits, badges or furni with the hotel's usual
services there. Core grants nothing by default. A handler that throws is logged and skipped.

## Free games and access

`IGameAccessPolicy` (`Turbo.Primitives.Games`) decides how many games a player has left
(`GetFreeGamesLeftAsync(playerId, gameTypeId, ct)`, `-1` unlimited) and is asked before a join.
The default reads the settings below and counts the games the player started since midnight UTC.
A plugin replaces it by declaring a public class implementing `IGameAccessPolicy`, found and
registered as games are (the last one loaded wins; unloading the plugin brings the default back).

## Settings (`Turbo:Games`, panel Settings, live)

| Setting | Default | |
| --- | --- | --- |
| `Enabled` | `true` | Off: the directory answers `1` (not available) and joins fail. |
| `DefaultGame` | `""` | The `Key` quick join plays; empty is the registered game with the lowest type id. |
| `FreeGamesPerDay` | `3` | Games a player may start per day; `-1` unlimited (official client constant `3`). |
| `ClubFreeGamesPerDay` | `30` | The same for Habbo Club members (official recordings show 30 for a member, 3 for a new account). |
| `CountdownSeconds` | `10` | The lobby countdown once the minimum players are in. |
| `LeaderboardMaxEntries` | `50` | Most entries one leaderboard request is answered with. |
| `LeaderboardMaxWeeksBack` | `4` | Weeks back a weekly leaderboard can be read (ISO weeks, Monday first). |

## Where it lives

- `Turbo.Primitives/Games`: the contracts above (`IRoomGame`, `GameDefinition`, `GameLaunch`,
  `GameArena`, `GameResult`, `IGameRegistry`, `IGameResultService`, `IGameRewardHandler`,
  `IGameAccessPolicy`, `IGameLeaderboardService`, `Grains/IGameDirectoryGrain`).
- `Turbo.Rooms/Games`: the registry, the assembly scan (`GameFeatureProcessor`), the directory
  grain (lobbies, countdown, launch) and the score store (`GameScoreStore`, table
  `game_player_results`); settings in `Turbo.Rooms/Configuration/GameCenterConfig.cs`.
- `Turbo.PacketHandlers/Game`: the `Game2*` handlers, orchestration only.
