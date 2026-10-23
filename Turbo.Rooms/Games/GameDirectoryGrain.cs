using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using Orleans.Runtime;
using Turbo.Primitives.Games;
using Turbo.Primitives.Games.Grains;
using Turbo.Primitives.Games.Snapshots;
using Turbo.Primitives.Messages.Outgoing.Game.Directory;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Rooms.Configuration;

namespace Turbo.Rooms.Games;

/// <summary>
/// The game centre, one grain for the hotel (<c>docs/plugin-games.md</c>). A join goes into the
/// game's open lobby, or opens one; a lobby with its minimum players counts down
/// (<c>Game2StartCounter</c>) and at the end of the count its game opens an arena room, every
/// player is sent <c>Game2GameStarted</c> and forwarded there, and the game counts as played.
/// Lobbies live in memory only: a lobby is a minute's wait, not something to keep.
/// </summary>
internal sealed class GameDirectoryGrain(
    GameRegistry registry,
    GameScoreStore scores,
    DefaultGameAccessPolicy defaultAccess,
    IOptionsMonitor<GameCenterConfig> config,
    IGrainFactory grainFactory,
    TimeProvider clock,
    ILogger<GameDirectoryGrain> logger
) : Grain, IGameDirectoryGrain
{
    private readonly GameDirectoryLiveState _state = new();

    private IGameAccessPolicy Access => registry.AccessPolicy ?? defaultAccess;

    public async Task<GameDirectoryStatusSnapshot> GetDirectoryStatusAsync(
        PlayerId playerId,
        CancellationToken ct
    )
    {
        var game = DefaultGame();

        if (game is null)
            return new GameDirectoryStatusSnapshot
            {
                Status = GameCenter.DIRECTORY_UNAVAILABLE,
                BlockLength = 0,
                GamesPlayed = 0,
                FreeGamesLeft = 0,
            };

        var status = await GetAccountStatusAsync(playerId, game.Definition.GameTypeId, ct);

        return new GameDirectoryStatusSnapshot
        {
            Status = GameCenter.DIRECTORY_AVAILABLE,
            BlockLength = 0,
            GamesPlayed = status.GamesPlayedTotal,
            FreeGamesLeft = status.FreeGamesLeft,
        };
    }

    public async Task<GameAccountStatusSnapshot> GetAccountStatusAsync(
        PlayerId playerId,
        int gameTypeId,
        CancellationToken ct
    )
    {
        var (total, _) = await scores.CountGamesAsync(playerId, gameTypeId, ct);
        var left = await Access.GetFreeGamesLeftAsync(playerId, gameTypeId, ct);

        return new GameAccountStatusSnapshot
        {
            GameTypeId = gameTypeId,
            FreeGamesLeft = left,
            GamesPlayedTotal = total,
        };
    }

    public async Task JoinAsync(PlayerId playerId, string? key, CancellationToken ct)
    {
        var game = string.IsNullOrEmpty(key) ? DefaultGame() : registry.Find(key);

        if (game is null || !config.CurrentValue.Enabled)
        {
            await FailJoinAsync(playerId, GameCenter.JOIN_FAILED_GENERIC, ct);
            return;
        }

        if (_state.LobbyByPlayer.ContainsKey(playerId))
        {
            await FailJoinAsync(playerId, GameCenter.JOIN_FAILED_ALREADY_IN_GAME, ct);
            return;
        }

        var definition = game.Definition;
        var left = await Access.GetFreeGamesLeftAsync(playerId, definition.GameTypeId, ct);

        if (left == 0)
        {
            await FailJoinAsync(playerId, GameCenter.JOIN_FAILED_NO_FREE_GAMES, ct);
            return;
        }

        _state.GameByPlayer.Remove(playerId);

        var summary = await grainFactory.GetPlayerGrain(playerId).GetSummaryAsync(ct);

        // The await above let another join in: check again before taking the place.
        if (_state.LobbyByPlayer.ContainsKey(playerId))
            return;

        var lobby = _state.Lobbies.FirstOrDefault(open =>
            open.Game == game && open.Players.Count < Math.Max(1, definition.MaxPlayers)
        );

        if (lobby is null)
        {
            lobby = new GameDirectoryLiveState.Lobby(game, ++_state.LastGameId, summary.Name);
            _state.Lobbies.Add(lobby);
        }

        var teams = Math.Max(1, definition.NumberOfTeams);
        var player = new GameLobbyPlayerSnapshot
        {
            PlayerId = playerId,
            Name = summary.Name,
            Figure = summary.Figure,
            Gender = summary.Gender.ToLegacyString(),
            TeamId = lobby.Players.Count % teams + 1,
        };
        var others = lobby.Players.Select(other => other.PlayerId).ToList();

        lobby.Players.Add(player);
        _state.LobbyByPlayer[playerId] = lobby;

        await grainFactory.SendComposerToPlayerAsync(
            playerId,
            new Game2GameCreatedMessageComposer { Lobby = Snapshot(lobby) },
            ct
        );
        await grainFactory.SendComposerToPlayersAsync(
            others,
            new Game2UserJoinedGameMessageComposer { Player = player },
            ct
        );

        if (lobby.StartsAt is null && lobby.Players.Count >= Math.Max(1, definition.MinPlayers))
            await StartCountdownAsync(lobby, ct);
        else if (lobby.StartsAt is { } startsAt)
            await grainFactory.SendComposerToPlayerAsync(
                playerId,
                new Game2StartCounterMessageMessageComposer
                {
                    CountDownLength = SecondsUntil(startsAt),
                },
                ct
            );
    }

    public async Task LeaveAsync(PlayerId playerId, CancellationToken ct)
    {
        if (_state.LobbyByPlayer.Remove(playerId, out var lobby))
        {
            lobby.Players.RemoveAll(player => player.PlayerId == playerId);

            var left = new Game2UserLeftGameMessageMessageComposer { PlayerId = playerId };
            await grainFactory.SendComposerToPlayersAsync([playerId, .. Ids(lobby)], left, ct);

            if (lobby.Players.Count == 0)
            {
                CloseLobby(lobby);
                return;
            }

            if (
                lobby.StartsAt is not null
                && lobby.Players.Count < Math.Max(1, lobby.Game.Definition.MinPlayers)
            )
            {
                lobby.StartsAt = null;
                lobby.Timer?.Dispose();
                lobby.Timer = null;

                await grainFactory.SendComposerToPlayersAsync(
                    Ids(lobby),
                    new Game2StopCounterMessageMessageComposer(),
                    ct
                );
            }

            return;
        }

        if (_state.GameByPlayer.Remove(playerId, out var launched))
        {
            try
            {
                await launched.Game.OnPlayerLeftAsync(launched.Launch, playerId, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Game {Key} failed on player {PlayerId} leaving",
                    launched.Game.Definition.Key,
                    playerId
                );
            }
        }
    }

    /// <summary>Starts every lobby whose countdown is over. The countdown timer calls it.</summary>
    internal async Task LaunchDueAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var due = _state.Lobbies.Where(lobby => lobby.StartsAt is { } at && at <= now).ToList();

        foreach (var lobby in due)
            await LaunchAsync(lobby, ct);
    }

    private async Task LaunchAsync(GameDirectoryLiveState.Lobby lobby, CancellationToken ct)
    {
        CloseLobby(lobby);

        foreach (var player in lobby.Players)
            _state.LobbyByPlayer.Remove(player.PlayerId);

        var players = Ids(lobby);

        // The game left with its plugin while the lobby counted down.
        if (!registry.Games.Contains(lobby.Game))
        {
            await grainFactory.SendComposerToPlayersAsync(
                players,
                new Game2GameCancelledMessageMessageComposer(),
                ct
            );
            return;
        }

        var launch = new GameLaunch
        {
            GameId = lobby.GameId,
            Definition = lobby.Game.Definition,
            Players =
            [
                .. lobby.Players.Select(player => new GameLaunchPlayer(
                    player.PlayerId,
                    player.Name,
                    player.Figure,
                    player.Gender,
                    player.TeamId
                )),
            ],
        };

        GameArena? arena = null;

        try
        {
            arena = await lobby.Game.OpenArenaAsync(launch, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Game {Key} failed to open an arena for game {GameId}",
                lobby.Game.Definition.Key,
                lobby.GameId
            );
        }

        if (arena is null || arena.RoomId.Value <= 0)
        {
            await grainFactory.SendComposerToPlayersAsync(
                players,
                new Game2StartingGameFailedMessageMessageComposer { Reason = 1 },
                ct
            );
            return;
        }

        await scores.RecordStartAsync(launch, ct);

        foreach (var player in players)
            _state.GameByPlayer[player] = new GameDirectoryLiveState.Launched(lobby.Game, launch);

        await grainFactory.SendComposerToPlayersAsync(
            players,
            new Game2GameStartedMessageComposer { Lobby = Snapshot(lobby) },
            ct
        );
        await Task.WhenAll(
            players.Select(player =>
                grainFactory.ForwardPlayerToRoomAsync(
                    player,
                    arena.RoomId,
                    RoomEntrySnapshot.Default,
                    ct
                )
            )
        );
    }

    private async Task StartCountdownAsync(GameDirectoryLiveState.Lobby lobby, CancellationToken ct)
    {
        var seconds = Math.Max(
            0,
            lobby.Game.Definition.CountdownSeconds ?? config.CurrentValue.CountdownSeconds
        );

        lobby.StartsAt = clock.GetUtcNow().AddSeconds(seconds);
        lobby.Timer = this.RegisterGrainTimer(
            static async (self, ct) => await self.LaunchDueAsync(ct),
            this,
            new GrainTimerCreationOptions
            {
                DueTime = TimeSpan.FromSeconds(seconds),
                Period = Timeout.InfiniteTimeSpan,
                KeepAlive = true,
            }
        );

        await grainFactory.SendComposerToPlayersAsync(
            Ids(lobby),
            new Game2StartCounterMessageMessageComposer { CountDownLength = seconds },
            ct
        );
    }

    private void CloseLobby(GameDirectoryLiveState.Lobby lobby)
    {
        lobby.Timer?.Dispose();
        lobby.Timer = null;
        lobby.StartsAt = null;
        _state.Lobbies.Remove(lobby);
    }

    private Task FailJoinAsync(PlayerId playerId, int reason, CancellationToken ct) =>
        grainFactory.SendComposerToPlayerAsync(
            playerId,
            new Game2JoiningGameFailedMessageMessageComposer { Reason = reason },
            ct
        );

    private IRoomGame? DefaultGame()
    {
        var settings = config.CurrentValue;

        if (!settings.Enabled)
            return null;

        return string.IsNullOrEmpty(settings.DefaultGame)
            ? registry.Games.OrderBy(game => game.Definition.GameTypeId).FirstOrDefault()
            : registry.Find(settings.DefaultGame);
    }

    private int SecondsUntil(DateTimeOffset at) =>
        Math.Max(0, (int)Math.Ceiling((at - clock.GetUtcNow()).TotalSeconds));

    private static List<PlayerId> Ids(GameDirectoryLiveState.Lobby lobby) =>
        [.. lobby.Players.Select(player => player.PlayerId)];

    private static GameLobbySnapshot Snapshot(GameDirectoryLiveState.Lobby lobby) =>
        new()
        {
            GameId = lobby.GameId,
            LevelName = lobby.Game.Definition.LevelName,
            GameType = lobby.Game.Definition.GameTypeId,
            FieldType = lobby.Game.Definition.FieldType,
            NumberOfTeams = Math.Max(1, lobby.Game.Definition.NumberOfTeams),
            MaximumPlayers = lobby.Game.Definition.MaxPlayers,
            OwningPlayerName = lobby.OwnerName,
            LevelEntryId = 0,
            Players = [.. lobby.Players],
        };

    public override async Task OnActivateAsync(CancellationToken ct)
    {
        try
        {
            _state.LastGameId = await scores.GetLastGameIdAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to read the last game id");
        }
    }
}

/// <summary>The game centre's lobbies and started games, in memory.</summary>
internal sealed class GameDirectoryLiveState
{
    public int LastGameId { get; set; }

    public List<Lobby> Lobbies { get; } = [];

    public Dictionary<PlayerId, Lobby> LobbyByPlayer { get; } = [];

    public Dictionary<PlayerId, Launched> GameByPlayer { get; } = [];

    internal sealed class Lobby(IRoomGame game, int gameId, string ownerName)
    {
        public IRoomGame Game { get; } = game;

        public int GameId { get; } = gameId;

        public string OwnerName { get; } = ownerName;

        public List<GameLobbyPlayerSnapshot> Players { get; } = [];

        public DateTimeOffset? StartsAt { get; set; }

        public IDisposable? Timer { get; set; }
    }

    internal sealed record Launched(IRoomGame Game, GameLaunch Launch);
}
