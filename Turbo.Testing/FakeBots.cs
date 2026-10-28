using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Contracts.Hotel;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;

namespace Turbo.Testing;

/// <summary>A bot placed in a <see cref="FakeRoom"/>. Its tile, dance and sign are what it was last told.</summary>
public sealed class FakeBot
{
    internal FakeBot(int id, string name, FakePlayer owner, RoomId room, int x, int y)
    {
        Id = id;
        Name = name;
        Owner = owner;
        Room = room;
        X = x;
        Y = y;
    }

    public int Id { get; }

    public string Name { get; set; }

    public string Motto { get; set; } = string.Empty;

    public FakePlayer Owner { get; }

    public RoomId Room { get; }

    public int X { get; set; }

    public int Y { get; set; }

    /// <summary>The furni item it walked onto last, if its last walk was to one.</summary>
    public int? OnFurni { get; set; }

    public int Dance { get; set; }

    public int? Sign { get; set; }

    /// <summary>Every gesture it made, in order.</summary>
    public List<HotelBotGesture> Gestures { get; } = [];

    public override string ToString() => $"{Name} ({Id})";
}

/// <summary>A line a <see cref="FakeBot"/> said: to the room, shouted, or whispered to one player.</summary>
public sealed record FakeBotLine(
    int BotId,
    RoomId Room,
    string Text,
    bool Shout,
    PlayerId? WhisperedTo
);

/// <summary>
/// <see cref="IHotelBots"/> in memory, with the hotel's rules: a room no one is in has no bots out
/// (every order answers false), a bot says at most ten lines in five seconds of the fake's clock,
/// and a walk arrives at once.
/// </summary>
public sealed class FakeBots : IHotelBots
{
    private const int FLOOD_MAX_LINES = 10;
    private static readonly TimeSpan FloodWindow = TimeSpan.FromSeconds(5);

    private readonly FakeHotel _hotel;
    private readonly List<FakeBot> _bots = [];
    private readonly List<FakeBotLine> _lines = [];
    private readonly Dictionary<int, Queue<DateTimeOffset>> _recent = [];

    internal FakeBots(FakeHotel hotel) => _hotel = hotel;

    public IReadOnlyList<FakeBot> All => _bots;

    /// <summary>Every line a bot said or whispered, in order.</summary>
    public IReadOnlyList<FakeBotLine> Lines => _lines;

    /// <summary>The next bot's id; ids count up from here.</summary>
    public int NextId { get; set; } = 1;

    /// <summary>Places a bot of <paramref name="owner"/> in the room on a tile.</summary>
    public FakeBot Add(FakeRoom room, string name, FakePlayer owner, int x = 0, int y = 0)
    {
        var bot = new FakeBot(NextId++, name, owner, room.Id, x, y);

        _bots.Add(bot);

        return bot;
    }

    public Task<IReadOnlyList<HotelBot>> InRoomAsync(RoomId room, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<HotelBot>>(
            _hotel.Rooms.IsPopulated(room)
                ?
                [
                    .. _bots
                        .Where(x => x.Room == room)
                        .Select(x => new HotelBot(
                            x.Id,
                            x.Name,
                            x.Motto,
                            x.Owner.Id,
                            x.Owner.Name,
                            room,
                            x.X,
                            x.Y
                        )),
                ]
                : []
        );

    public Task<bool> SayAsync(
        RoomId room,
        int botId,
        string text,
        bool shout,
        CancellationToken ct
    ) =>
        Task.FromResult(
            Find(room, botId) is { } bot
                && !string.IsNullOrWhiteSpace(text)
                && TryTakeLine(bot)
                && Said(new FakeBotLine(bot.Id, room, text, shout, null))
        );

    public Task<bool> WhisperAsync(
        RoomId room,
        int botId,
        PlayerId player,
        string text,
        CancellationToken ct
    ) =>
        Task.FromResult(
            Find(room, botId) is { } bot
                && !string.IsNullOrWhiteSpace(text)
                && _hotel.Rooms.IsIn(room, player)
                && Said(new FakeBotLine(bot.Id, room, text, false, player))
        );

    public Task<bool> WalkToAsync(RoomId room, int botId, int x, int y, CancellationToken ct)
    {
        if (Find(room, botId) is not { } bot || x < 0 || y < 0 || (bot.X == x && bot.Y == y))
            return Task.FromResult(false);

        (bot.X, bot.Y, bot.OnFurni) = (x, y, null);

        return Task.FromResult(true);
    }

    public Task<bool> WalkToFurniAsync(RoomId room, int botId, int itemId, CancellationToken ct)
    {
        if (
            Find(room, botId) is not { } bot
            || _hotel
                .Rooms.All.First(x => x.Id == room)
                .Furni.FirstOrDefault(x => x.ItemId == itemId)
                is not { } furni
        )
            return Task.FromResult(false);

        (bot.X, bot.Y, bot.OnFurni) = (furni.X, furni.Y, itemId);

        return Task.FromResult(true);
    }

    public Task<bool> GestureAsync(
        RoomId room,
        int botId,
        HotelBotGesture gesture,
        CancellationToken ct
    )
    {
        if (Find(room, botId) is not { } bot || !Enum.IsDefined(gesture))
            return Task.FromResult(false);

        bot.Gestures.Add(gesture);

        return Task.FromResult(true);
    }

    public Task<bool> DanceAsync(RoomId room, int botId, int dance, CancellationToken ct)
    {
        if (Find(room, botId) is not { } bot || dance is < 0 or > 4 || bot.Dance == dance)
            return Task.FromResult(false);

        bot.Dance = dance;

        return Task.FromResult(true);
    }

    public Task<bool> SignAsync(RoomId room, int botId, int sign, CancellationToken ct)
    {
        if (Find(room, botId) is not { } bot || sign is < 0 or > 17)
            return Task.FromResult(false);

        bot.Sign = sign;

        return Task.FromResult(true);
    }

    /// <summary>The lines the bot said to the room (whispers left out).</summary>
    public IReadOnlyList<string> SaidBy(int botId) =>
        [.. _lines.Where(x => x.BotId == botId && x.WhisperedTo is null).Select(x => x.Text)];

    /// <summary>Fails unless the bot said <paramref name="text"/> to the room.</summary>
    public void AssertSaid(int botId, string text)
    {
        if (!SaidBy(botId).Contains(text))
            throw new HotelAssertionException(
                $"Bot {botId} did not say \"{text}\"; it said {FakeHotelText.List(SaidBy(botId).Select(x => $"\"{x}\""))}."
            );
    }

    private FakeBot? Find(RoomId room, int botId) =>
        _hotel.Rooms.IsPopulated(room)
            ? _bots.FirstOrDefault(x => x.Room == room && x.Id == botId)
            : null;

    private bool TryTakeLine(FakeBot bot)
    {
        var now = _hotel.Clock.GetUtcNow();

        if (!_recent.TryGetValue(bot.Id, out var lines))
            _recent[bot.Id] = lines = new Queue<DateTimeOffset>();

        while (lines.Count > 0 && now - lines.Peek() >= FloodWindow)
            lines.Dequeue();

        if (lines.Count >= FLOOD_MAX_LINES)
            return false;

        lines.Enqueue(now);

        return true;
    }

    private bool Said(FakeBotLine line)
    {
        _lines.Add(line);

        return true;
    }
}
