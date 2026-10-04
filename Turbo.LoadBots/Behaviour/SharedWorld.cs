using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Turbo.LoadBots.Behaviour;

/// <summary>What a bot built its room for.</summary>
public enum RoomPurpose
{
    Decor,
    Architecture,
    WiredLab,
    Game,
}

/// <summary>A room a bot built and opened to the others.</summary>
public sealed record PublicRoom(int RoomId, string OwnerName, RoomPurpose Purpose);

/// <summary>
/// A game room's layout: where the pads are and what the wired says, so a player knows what
/// to walk onto and what to expect to hear, and the host's game timer.
/// </summary>
public sealed record GameRoom(
    int RoomId,
    string HostName,
    IReadOnlyList<(int X, int Y)> Pads,
    string GoalText,
    string StartText,
    string EndText,
    int CounterObjectId
);

/// <summary>
/// What the bots know of each other, as players would by word of mouth: whose rooms are worth
/// a visit and which games are on. In-process only; finding rooms through the navigator is a
/// separate path the visitors also take.
/// </summary>
public sealed class SharedWorld(IReadOnlyCollection<string> wiredLogics)
{
    private readonly ConcurrentDictionary<int, PublicRoom> _rooms = new();
    private readonly ConcurrentDictionary<int, GameRoom> _games = new();
    private readonly string[] _wiredLogics = [.. wiredLogics.Order(StringComparer.Ordinal)];
    private readonly ConcurrentDictionary<string, string> _wiredOutcomes = new(
        StringComparer.Ordinal
    );
    private int _wiredCursor = -1;

    public void Publish(PublicRoom room) => _rooms[room.RoomId] = room;

    public void PublishGame(GameRoom game)
    {
        _games[game.RoomId] = game;
        Publish(new PublicRoom(game.RoomId, game.HostName, RoomPurpose.Game));
    }

    public void Withdraw(int roomId)
    {
        _rooms.TryRemove(roomId, out _);
        _games.TryRemove(roomId, out _);
    }

    public PublicRoom? RandomRoom(Random random, string notOwnedBy)
    {
        var candidates = _rooms.Values.Where(x => x.OwnerName != notOwnedBy).ToList();

        return candidates.Count == 0 ? null : candidates[random.Next(candidates.Count)];
    }

    public GameRoom? RandomGame(Random random)
    {
        var games = _games.Values.ToList();

        return games.Count == 0 ? null : games[random.Next(games.Count)];
    }

    public PublicRoom? Room(int roomId) => _rooms.GetValueOrDefault(roomId);

    public GameRoom? Game(int roomId) => _games.GetValueOrDefault(roomId);

    /// <summary>
    /// The next wired logics to try, handed out round-robin across every engineer so the
    /// fleet covers each box before repeating any.
    /// </summary>
    public IReadOnlyList<string> NextWiredLogics(int count)
    {
        if (_wiredLogics.Length == 0)
            return [];

        var logics = new List<string>(count);

        for (var i = 0; i < Math.Min(count, _wiredLogics.Length); i++)
        {
            var index = Interlocked.Increment(ref _wiredCursor);
            logics.Add(_wiredLogics[index % _wiredLogics.Length]);
        }

        return logics;
    }

    /// <summary>Remembers how the last save of each wired logic went, for the report.</summary>
    public void RecordWiredOutcome(string logic, string outcome) => _wiredOutcomes[logic] = outcome;

    public IReadOnlyDictionary<string, string> WiredOutcomes() =>
        new SortedDictionary<string, string>(_wiredOutcomes, StringComparer.Ordinal);

    /// <summary>The wired logics no engineer has got to yet.</summary>
    public IReadOnlyList<string> UntriedWiredLogics() =>
        [.. _wiredLogics.Where(x => !_wiredOutcomes.ContainsKey(x))];
}
