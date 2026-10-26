using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Achievements;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Games;

namespace Turbo.Rooms.Grains.Systems;

/// <summary>
/// How a room game finishes. A Battle Banzai game whose clock reached 0 went on in beats 500 ms
/// apart (official client capture 2026-10-26, session 012541, three games, 3/3): on the first the
/// winning team's players waved (Expression 1, 488-586 ms after the 0) and the achievements of
/// the game came, then the field showed who won (<see cref="RoomGameChange.EndBeat"/>). Teams,
/// their effects and the scoreboards stayed as they were until the next start.
/// </summary>
public sealed partial class RoomGameSystem
{
    /// <summary>The beats of a finish: the tiles' last flash came on the 12th (3/3).</summary>
    public const int END_BEATS = 12;

    private const int END_BEAT_MS = 500;

    // The game is no furni; its finish keeps a timer key no room object has.
    private static readonly RoomObjectId FINISH_TIMER_KEY = int.MinValue + 1;

    /// <summary>
    /// The achievements a game hands its players at the end, by the kind of team they played in:
    /// "For gaining %limit% participation points in Battle Banzai" and "... victory points"
    /// (<c>badge_desc_ACH_BattleBallPlayer</c>, <c>_BattleBallWinner</c>). Only the games here
    /// finish with waves and achievements.
    /// </summary>
    private static readonly Dictionary<WiredTeamType, (string Player, string Winner)> GameRecords =
        new() { [WiredTeamType.BattleBanzai] = ("battleballplayer", "battleballwinner") };

    private const string PLAYER_EXPERIENCE = "gameplayerexperience";
    private const string AUTHOR_EXPERIENCE = "gameauthorexperience";
    private const string TILES_LOCKED = "battleballtileslocked";

    private readonly Dictionary<RoomObjectId, int> _tilesLockedByAvatarId = [];

    /// <summary>The beat of the last game's finish, 1 to <see cref="END_BEATS"/>; 0 before it.</summary>
    public int EndBeat { get; private set; }

    /// <summary>The team that won the last game; none when no team scored.</summary>
    public GameTeamType WinningTeam { get; private set; }

    private int _playedSeconds;

    /// <summary>
    /// Banzai tiles a player locked, the ones their lock closed in included: the achievement got
    /// them all at the game's end (G1 9 locks and 1 fill +10, G3 8 and 1 +9, G2 none; 2/2).
    /// </summary>
    public void CountTilesLocked(RoomObjectId avatarId, int tiles)
    {
        _tilesLockedByAvatarId.TryGetValue(avatarId, out var counted);
        _tilesLockedByAvatarId[avatarId] = counted + tiles;
    }

    private void ResetFinish()
    {
        TimerSystem.Cancel(FINISH_TIMER_KEY);
        _tilesLockedByAvatarId.Clear();
        EndBeat = 0;
        WinningTeam = GameTeamType.None;
    }

    private void BeginFinish()
    {
        EndBeat = 0;
        WinningTeam = FindWinner();
        _playedSeconds = (int)Math.Max(0, Math.Round((UtcNow - _startedAt).TotalSeconds));

        TimerSystem.Schedule(FINISH_TIMER_KEY, END_BEAT_MS, OnEndBeatAsync);
    }

    private async Task OnEndBeatAsync(CancellationToken ct)
    {
        if (IsGameRunning)
            return;

        EndBeat++;

        if (EndBeat == 1)
        {
            WaveWinners();
            await RecordGameAsync(ct);
        }

        await UpdateGameFurniAsync(RoomGameChange.EndBeat, ct);

        if (EndBeat < END_BEATS)
            TimerSystem.Schedule(FINISH_TIMER_KEY, END_BEAT_MS, OnEndBeatAsync);
    }

    /// <summary>
    /// The team placed first among the teams that played. Inference: a game nobody scored in has
    /// no winner (every recorded game had one, G1 10:2, G2 1:0, G3 9:0).
    /// </summary>
    private GameTeamType FindWinner()
    {
        var winner = GameTeamType.None;

        for (var team = GameTeamType.Red; team <= GameTeamType.Yellow; team++)
        {
            if (GetScore(team) <= 0 || !GameMembers(team).Any())
                continue;

            if (winner == GameTeamType.None || PlacesAbove((int)team, (int)winner))
                winner = team;
        }

        return winner;
    }

    /// <summary>The avatars of a team who joined it for a game in <see cref="GameRecords"/>.</summary>
    private IEnumerable<RoomObjectId> GameMembers(GameTeamType team) =>
        GetTeamMembers(team).Where(id => GameRecords.ContainsKey(GetTeamType(id)));

    // Only the winners waved; the losers did not (3/3).
    private void WaveWinners()
    {
        if (WinningTeam == GameTeamType.None)
            return;

        foreach (var avatarId in GameMembers(WinningTeam).ToList())
            AvatarModule
                .SetAvatarExpressionAsync(
                    avatarId,
                    AvatarExpressionType.Wave,
                    CancellationToken.None
                )
                .LogAndForget(Logger, "wave the winners in room {RoomId}", _roomGrain.RoomId);
    }

    /// <summary>
    /// The game's achievements, all at its end and none while it ran (official client capture
    /// 2026-10-26, session 012541; progress before and after G2 and G3, 2/2 each): every player
    /// gets their game's player record by the seconds played (G2 300 s +300, G3 60 s +60) and the
    /// Banzai tiles they locked; the room's owner gets <c>GameAuthorExperience</c> by the players
    /// times the seconds (2 players: G2 +600, G3 +120); the winners get their game's winner record
    /// and <c>GamePlayerExperience</c>, by the same product (G3 +120 each, 1/1; nothing when lost).
    /// Inference: every recorded game had two players, so "the players times the seconds" is not
    /// told apart from "twice the seconds" by them.
    /// </summary>
    private async Task RecordGameAsync(CancellationToken ct)
    {
        var players =
            new List<(PlayerId Id, RoomObjectId AvatarId, GameTeamType Team, string Record)>();

        for (var team = GameTeamType.Red; team <= GameTeamType.Yellow; team++)
        {
            foreach (var avatarId in GameMembers(team))
            {
                if (
                    AvatarModule.TryGetAvatar(avatarId, out var avatar)
                    && avatar is IRoomPlayer player
                )
                    players.Add(
                        (player.PlayerId, avatarId, team, GameRecords[GetTeamType(avatarId)].Player)
                    );
            }
        }

        if (players.Count == 0)
            return;

        var points = players.Count * _playedSeconds;
        var facts = new List<(PlayerId, string Source, string Value, int Amount)>();

        foreach (var (playerId, avatarId, team, record) in players)
        {
            facts.Add((playerId, AchievementSources.ROOM_GAME_POINTS, record, _playedSeconds));

            if (_tilesLockedByAvatarId.TryGetValue(avatarId, out var tiles) && tiles > 0)
                facts.Add(
                    (playerId, AchievementSources.BATTLEBALL_TILE_LOCKED, TILES_LOCKED, tiles)
                );

            if (team != WinningTeam)
                continue;

            var winner = GameRecords[GetTeamType(avatarId)].Winner;

            facts.Add((playerId, AchievementSources.ROOM_GAME_POINTS, winner, points));
            facts.Add((playerId, AchievementSources.ROOM_GAME_POINTS, PLAYER_EXPERIENCE, points));
        }

        if (State.RoomSnapshot?.OwnerId is { Value: > 0 } ownerId)
            facts.Add((ownerId, AchievementSources.ROOM_GAME_POINTS, AUTHOR_EXPERIENCE, points));

        await using var db = await _roomGrain._dbCtxFactory.CreateDbContextAsync(ct);

        foreach (var (playerId, source, value, amount) in facts.Where(x => x.Amount > 0))
            _roomGrain._achievementFacts.Record(
                db,
                playerId,
                new()
                {
                    Source = source,
                    Value = value,
                    Amount = amount,
                    OperationId = Guid.NewGuid().ToString("N"),
                    OccurredAtUtc = UtcNow.UtcDateTime,
                }
            );

        await db.SaveChangesAsync(ct);
    }
}
