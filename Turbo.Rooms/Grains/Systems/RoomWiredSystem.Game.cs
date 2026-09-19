using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Rooms.Grains.Systems;

/// <summary>
/// Teams, scores and the game clock the team wired shares: joining a team wears the team
/// effect, scores are per team and reset when a game starts, and "game starts" / "game ends"
/// triggers fire around a timed game.
/// </summary>
public sealed partial class RoomWiredSystem
{
    private readonly Dictionary<PlayerId, WiredTeamType> _teamByPlayerId = [];
    private readonly int[] _teamScores = new int[5];
    private readonly Dictionary<(RoomObjectId actionId, PlayerId playerId), int> _scoreGrants = [];
    private long _gameEndsAtMs = -1;

    public bool IsGameRunning { get; private set; }

    public WiredTeamType GetTeam(PlayerId playerId) =>
        _teamByPlayerId.TryGetValue(playerId, out var team) ? team : WiredTeamType.None;

    public int GetScore(WiredTeamType team) =>
        team is >= WiredTeamType.Red and <= WiredTeamType.Yellow ? _teamScores[(int)team] : 0;

    /// <summary>1 for the leading team, 4 for the last; ties share the better placement.</summary>
    public int GetPlacement(WiredTeamType team)
    {
        if (team is < WiredTeamType.Red or > WiredTeamType.Yellow)
            return 4;

        var score = _teamScores[(int)team];
        var better = 0;

        for (var i = 1; i <= 4; i++)
        {
            if (i != (int)team && _teamScores[i] > score)
                better++;
        }

        return better + 1;
    }

    public IEnumerable<PlayerId> GetTeamMembers(WiredTeamType team) =>
        _teamByPlayerId.Where(x => x.Value == team).Select(x => x.Key);

    public async Task<bool> JoinTeamAsync(
        PlayerId playerId,
        WiredTeamType team,
        CancellationToken ct
    )
    {
        if (team is < WiredTeamType.Red or > WiredTeamType.Yellow)
            return false;

        if (!_roomGrain._state.AvatarsByPlayerId.TryGetValue(playerId, out var objectId))
            return false;

        if (GetTeam(playerId) == team)
            return true;

        _teamByPlayerId[playerId] = team;

        var effectIds = _roomGrain._roomConfig.WiredTeamEffectIds;
        var effectId = (int)team < effectIds.Length ? effectIds[(int)team] : 0;

        await _roomGrain.AvatarModule.SetAvatarEffectAsync(objectId, effectId, ct);

        return true;
    }

    public async Task<bool> LeaveTeamAsync(PlayerId playerId, CancellationToken ct)
    {
        if (!_teamByPlayerId.Remove(playerId))
            return false;

        if (_roomGrain._state.AvatarsByPlayerId.TryGetValue(playerId, out var objectId))
            await _roomGrain.AvatarModule.SetAvatarEffectAsync(objectId, 0, ct);

        return true;
    }

    /// <summary>
    /// Adds points to a team. <paramref name="timesPerGame"/> caps how often one action may
    /// score for one player during a game; zero means unlimited.
    /// </summary>
    public async Task<bool> GiveScoreAsync(
        WiredTeamType team,
        int points,
        RoomObjectId actionId,
        PlayerId playerId,
        int timesPerGame,
        CancellationToken ct
    )
    {
        if (team is < WiredTeamType.Red or > WiredTeamType.Yellow)
            return false;

        if (timesPerGame > 0)
        {
            var key = (actionId, playerId);

            _scoreGrants.TryGetValue(key, out var granted);

            if (granted >= timesPerGame)
                return false;

            _scoreGrants[key] = granted + 1;
        }

        var previous = _teamScores[(int)team];

        _teamScores[(int)team] = Math.Max(0, previous + points);

        await _roomGrain.PublishRoomEventAsync(
            new WiredScoreChangedEvent
            {
                RoomId = _roomGrain.RoomId,
                CausedBy = ActionContext.CreateForWired(_roomGrain.RoomId),
                Team = team,
                Score = _teamScores[(int)team],
                PreviousScore = previous,
            },
            ct
        );

        return true;
    }

    /// <summary>Starts a game; scores and per-game score limits reset.</summary>
    public async Task StartGameAsync(int durationSeconds, CancellationToken ct)
    {
        Array.Clear(_teamScores);
        _scoreGrants.Clear();

        IsGameRunning = true;
        _gameEndsAtMs = durationSeconds > 0 ? _roomGrain.NowMs() + durationSeconds * 1000L : -1;

        await _roomGrain.PublishRoomEventAsync(
            new WiredGameStartedEvent
            {
                RoomId = _roomGrain.RoomId,
                CausedBy = ActionContext.CreateForWired(_roomGrain.RoomId),
            },
            ct
        );
    }

    public async Task EndGameAsync(CancellationToken ct)
    {
        if (!IsGameRunning)
            return;

        IsGameRunning = false;
        _gameEndsAtMs = -1;

        await _roomGrain.PublishRoomEventAsync(
            new WiredGameEndedEvent
            {
                RoomId = _roomGrain.RoomId,
                CausedBy = ActionContext.CreateForWired(_roomGrain.RoomId),
            },
            ct
        );
    }

    private Task ProcessGameAsync(long now, CancellationToken ct)
    {
        if (!IsGameRunning || _gameEndsAtMs < 0 || now < _gameEndsAtMs)
            return Task.CompletedTask;

        return EndGameAsync(ct);
    }

    private void ForgetPlayer(PlayerId playerId)
    {
        _teamByPlayerId.Remove(playerId);

        foreach (var key in _scoreGrants.Keys.Where(x => x.playerId == playerId).ToList())
            _scoreGrants.Remove(key);
    }
}
