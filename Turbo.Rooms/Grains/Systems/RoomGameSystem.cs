using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Outgoing.Room.Session;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Events;
using Turbo.Primitives.Rooms.Events.Game;
using Turbo.Primitives.Rooms.Events.Player;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Rooms.Grains.Systems;

/// <summary>
/// The room's game: who is on which team, the team scores, and whether a game is running.
/// Joining a team wears the team effect, scores reset when a game starts, and the start, the end
/// and every score change are published as room events.
///
/// It knows nothing of who drives it. A game counter furni starts and ends the game, wired boxes
/// move players between teams and hand out points, and wired triggers listen for the events;
/// game furni that comes later (banzai, freeze) uses the same teams and scores.
/// </summary>
public sealed class RoomGameSystem(RoomGrain roomGrain) : IRoomEventListener
{
    private const int NO_EFFECT = 0;

    private readonly RoomGrain _roomGrain = roomGrain;

    private readonly Dictionary<PlayerId, GameTeamType> _teamByPlayerId = [];
    private readonly int[] _teamScores = new int[(int)GameTeamType.Yellow + 1];
    private readonly Dictionary<(RoomObjectId sourceId, PlayerId playerId), int> _scoreGrants = [];

    public bool IsGameRunning { get; private set; }

    public Task OnRoomEventAsync(RoomEvent evt, CancellationToken ct)
    {
        if (evt is PlayerLeftEvent left)
            ForgetPlayer(left.PlayerId);

        return Task.CompletedTask;
    }

    public GameTeamType GetTeam(PlayerId playerId) =>
        _teamByPlayerId.TryGetValue(playerId, out var team) ? team : GameTeamType.None;

    public int GetScore(GameTeamType team) => IsTeam(team) ? _teamScores[(int)team] : 0;

    /// <summary>1 for the leading team, 4 for the last; ties share the better placement.</summary>
    public int GetPlacement(GameTeamType team)
    {
        if (!IsTeam(team))
            return (int)GameTeamType.Yellow;

        var score = _teamScores[(int)team];
        var better = 0;

        for (var i = (int)GameTeamType.Red; i <= (int)GameTeamType.Yellow; i++)
        {
            if (i != (int)team && _teamScores[i] > score)
                better++;
        }

        return better + 1;
    }

    public IEnumerable<PlayerId> GetTeamMembers(GameTeamType team) =>
        _teamByPlayerId.Where(x => x.Value == team).Select(x => x.Key);

    public async Task<bool> JoinTeamAsync(
        PlayerId playerId,
        GameTeamType team,
        CancellationToken ct
    )
    {
        if (!IsTeam(team))
            return false;

        if (!_roomGrain.AvatarModule.TryGetPlayer(playerId, out var player))
            return false;

        if (GetTeam(playerId) == team)
            return true;

        _teamByPlayerId[playerId] = team;

        await _roomGrain.AvatarModule.SetAvatarEffectAsync(
            player.ObjectId,
            GetTeamEffectId(team),
            ct
        );
        await PublishTeamChangedAsync(playerId, team, ct);

        return true;
    }

    public async Task<bool> LeaveTeamAsync(PlayerId playerId, CancellationToken ct)
    {
        if (!_teamByPlayerId.Remove(playerId, out var team))
            return false;

        // Only the team colour is taken off. An effect the player put on since is theirs.
        if (
            _roomGrain.AvatarModule.TryGetPlayer(playerId, out var player)
            && player.EffectId == GetTeamEffectId(team)
        )
            await _roomGrain.AvatarModule.SetAvatarEffectAsync(player.ObjectId, NO_EFFECT, ct);

        await PublishTeamChangedAsync(playerId, GameTeamType.None, ct);

        return true;
    }

    /// <summary>
    /// Adds points to a team. <paramref name="timesPerGame"/> caps how often one source (the
    /// furni handing out the points) may score for one player during a game; zero means
    /// unlimited.
    /// </summary>
    /// <summary>
    /// Puts a team's score at a number, rather than adding to it: what writing the
    /// <c>@team.score</c> variable does. It is not a grant, so the per-game limits a scoring
    /// box obeys do not apply.
    /// </summary>
    public async Task<bool> SetScoreAsync(GameTeamType team, int score, CancellationToken ct)
    {
        if (!IsTeam(team))
            return false;

        var previous = _teamScores[(int)team];
        var next = Math.Max(0, score);

        if (next == previous)
            return false;

        _teamScores[(int)team] = next;

        await _roomGrain.PublishRoomEventAsync(
            new GameScoreChangedEvent
            {
                RoomId = _roomGrain.RoomId,
                CausedBy = ActionContext.CreateForSystem(_roomGrain.RoomId),
                Team = team,
                Score = next,
                PreviousScore = previous,
            },
            ct
        );

        return true;
    }

    public async Task<bool> GiveScoreAsync(
        GameTeamType team,
        int points,
        RoomObjectId sourceId,
        PlayerId playerId,
        int timesPerGame,
        CancellationToken ct
    )
    {
        if (!IsTeam(team))
            return false;

        if (timesPerGame > 0)
        {
            var key = (sourceId, playerId);

            _scoreGrants.TryGetValue(key, out var granted);

            if (granted >= timesPerGame)
                return false;

            _scoreGrants[key] = granted + 1;
        }

        var previous = _teamScores[(int)team];

        _teamScores[(int)team] = Math.Max(0, previous + points);

        await _roomGrain.PublishRoomEventAsync(
            new GameScoreChangedEvent
            {
                RoomId = _roomGrain.RoomId,
                CausedBy = ActionContext.CreateForSystem(_roomGrain.RoomId),
                Team = team,
                Score = _teamScores[(int)team],
                PreviousScore = previous,
            },
            ct
        );

        return true;
    }

    /// <summary>Starts a game; scores and per-game score limits reset. Whoever starts it times it.</summary>
    public async Task StartGameAsync(CancellationToken ct)
    {
        Array.Clear(_teamScores);
        _scoreGrants.Clear();

        IsGameRunning = true;

        await _roomGrain.PublishRoomEventAsync(
            new GameStartedEvent
            {
                RoomId = _roomGrain.RoomId,
                CausedBy = ActionContext.CreateForSystem(_roomGrain.RoomId),
            },
            ct
        );
    }

    public async Task EndGameAsync(CancellationToken ct)
    {
        if (!IsGameRunning)
            return;

        IsGameRunning = false;

        await _roomGrain.PublishRoomEventAsync(
            new GameEndedEvent
            {
                RoomId = _roomGrain.RoomId,
                CausedBy = ActionContext.CreateForSystem(_roomGrain.RoomId),
            },
            ct
        );
    }

    private Task PublishTeamChangedAsync(PlayerId playerId, GameTeamType team, CancellationToken ct)
    {
        // The client has a playing mode of its own (it hides what gets in the way of a game);
        // being on a team is what puts a player in it.
        _roomGrain
            ._grainFactory.SendComposerToPlayerAsync(
                playerId,
                new YouArePlayingGameMessageComposer { IsPlaying = team != GameTeamType.None },
                CancellationToken.None
            )
            .LogAndForget(_roomGrain._logger, $"tell player {playerId} whether they are playing");

        return _roomGrain.PublishRoomEventAsync(
            new GameTeamChangedEvent
            {
                RoomId = _roomGrain.RoomId,
                CausedBy = ActionContext.CreateForSystem(_roomGrain.RoomId),
                PlayerId = playerId,
                Team = team,
            },
            ct
        );
    }

    private static bool IsTeam(GameTeamType team) =>
        team is >= GameTeamType.Red and <= GameTeamType.Yellow;

    private int GetTeamEffectId(GameTeamType team)
    {
        var effectIds = _roomGrain._roomConfig.GameTeamEffectIds;

        return (int)team < effectIds.Length ? effectIds[(int)team] : NO_EFFECT;
    }

    private void ForgetPlayer(PlayerId playerId)
    {
        _teamByPlayerId.Remove(playerId);

        foreach (var key in _scoreGrants.Keys.Where(x => x.playerId == playerId).ToList())
            _scoreGrants.Remove(key);
    }
}
