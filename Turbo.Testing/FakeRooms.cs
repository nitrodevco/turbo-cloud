using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Contracts.Hotel;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;
using Turbo.Primitives.Plugins.Messages;
using Turbo.Primitives.Rooms;

namespace Turbo.Testing;

/// <summary>A room of a <see cref="FakeHotel"/>. Passes as its <see cref="RoomId"/>.</summary>
public sealed class FakeRoom
{
    internal FakeRoom(RoomId id, string name, FakePlayer owner)
    {
        Id = id;
        Name = name;
        Owner = owner;
    }

    public RoomId Id { get; }

    public string Name { get; set; }

    public string Description { get; set; } = string.Empty;

    public FakePlayer Owner { get; set; }

    /// <summary>On the navigator's public list, as a room in a shown Public tab category is too.</summary>
    public bool IsPublic { get; set; }

    /// <summary>Whether <see cref="IHotelRooms.LoadAsync"/> (or a player entering) loaded it.</summary>
    public bool Loaded { get; set; }

    public ISet<PlayerId> Players { get; } = new HashSet<PlayerId>();

    public static implicit operator RoomId(FakeRoom room) => room.Id;

    public override string ToString() => $"{Name} ({Id})";
}

/// <summary>The rooms of a <see cref="FakeHotel"/>, and what was sent and whispered in them.</summary>
public sealed class FakeRooms : IHotelRooms
{
    private readonly FakeHotel _hotel;
    private readonly List<FakeRoom> _rooms = [];
    private readonly List<(RoomId Room, object Message)> _sent = [];
    private readonly List<(RoomId Room, PlayerId Player, string Text)> _whispers = [];

    internal FakeRooms(FakeHotel hotel) => _hotel = hotel;

    public IReadOnlyList<FakeRoom> All => _rooms;

    public IReadOnlyList<(RoomId Room, object Message)> Sent => _sent;

    public IReadOnlyList<(RoomId Room, PlayerId Player, string Text)> Whispers => _whispers;

    /// <summary>The id the next <see cref="Add"/> gets (1 to start); set it to match the ids a test needs.</summary>
    public int NextId { get; set; } = 1;

    /// <summary>A new room, ids counting up from <see cref="NextId"/>.</summary>
    public FakeRoom Add(string name, FakePlayer owner, bool isPublic = false)
    {
        if (_rooms.Any(x => x.Id.Value == NextId))
            throw new ArgumentException($"The fake hotel already has a room {NextId}.");

        var room = new FakeRoom(new RoomId(NextId++), name, owner) { IsPublic = isPublic };

        _rooms.Add(room);

        return room;
    }

    /// <summary>Puts the player in the room (out of any other), as walking in does.</summary>
    public void Enter(FakeRoom room, FakePlayer player)
    {
        Leave(player);
        room.Players.Add(player.Id);
        room.Loaded = true;
    }

    public void Leave(FakePlayer player)
    {
        foreach (var room in _rooms)
            room.Players.Remove(player.Id);
    }

    public Task<HotelRoom?> GetAsync(RoomId room, CancellationToken ct) =>
        Task.FromResult(
            Find(room) is { } r
                ? new HotelRoom(r.Id, r.Name, r.Description, r.Owner.Id, r.Owner.Name, IsListed(r))
                : null
        );

    public Task<IReadOnlyCollection<RoomId>> PublicIdsAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyCollection<RoomId>>([.. _rooms.Where(IsListed).Select(x => x.Id)]);

    /// <summary>Marked public, or in a shown category of the Public tab.</summary>
    private bool IsListed(FakeRoom room) => room.IsPublic || _hotel.Navigator.Public.Lists(room.Id);

    public Task<IReadOnlyList<PlayerId>> PlayersAsync(RoomId room, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<PlayerId>>([
            .. Find(room)?.Players.Order() ?? Enumerable.Empty<PlayerId>(),
        ]);

    public Task<bool> SendAsync(RoomId room, IPluginMessage message, CancellationToken ct) =>
        Task.FromResult(Record(room, message));

    public Task<bool> SendAsync(RoomId room, IComposer composer, CancellationToken ct) =>
        Task.FromResult(Record(room, composer));

    public Task<bool> WhisperAsync(RoomId room, PlayerId player, string text, CancellationToken ct)
    {
        if (Find(room) is not { } r || !r.Players.Contains(player))
            return Task.FromResult(false);

        _whispers.Add((room, player, text));

        return Task.FromResult(true);
    }

    public Task LoadAsync(RoomId room, CancellationToken ct)
    {
        if (Find(room) is { } r)
            r.Loaded = true;

        return Task.CompletedTask;
    }

    /// <summary>What was sent to everyone in the room, in order.</summary>
    public IReadOnlyList<object> SentTo(RoomId room) =>
        [.. _sent.Where(x => x.Room == room).Select(x => x.Message)];

    public T AssertSent<T>(RoomId room)
        where T : class =>
        SentTo(room).OfType<T>().LastOrDefault()
        ?? throw new HotelAssertionException(
            $"Expected a {typeof(T).Name} sent to room {room}; it was sent {FakeHotelText.List(SentTo(room).Select(x => x.GetType().Name))}."
        );

    /// <summary>The whispers the player was sent in any room, in order.</summary>
    public IReadOnlyList<string> WhispersTo(PlayerId player) =>
        [.. _whispers.Where(x => x.Player == player).Select(x => x.Text)];

    public void AssertWhispered(PlayerId player, string text)
    {
        if (!WhispersTo(player).Contains(text))
            throw new HotelAssertionException(
                $"Expected the whisper \"{text}\" to player {player}; they were whispered {FakeHotelText.List(WhispersTo(player).Select(x => $"\"{x}\""))}."
            );
    }

    internal RoomId? RoomOf(PlayerId player) =>
        _rooms.FirstOrDefault(x => x.Players.Contains(player))?.Id;

    internal bool IsIn(RoomId room, PlayerId player) =>
        Find(room)?.Players.Contains(player) == true;

    internal bool IsPopulated(RoomId room) => Find(room) is { Players.Count: > 0 };

    private FakeRoom? Find(RoomId room) => _rooms.FirstOrDefault(x => x.Id == room);

    private bool Record(RoomId room, object message)
    {
        if (Find(room) is not { Players.Count: > 0 })
            return false;

        _sent.Add((room, message));

        return true;
    }
}
