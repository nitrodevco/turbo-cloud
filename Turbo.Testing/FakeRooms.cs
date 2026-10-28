using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Contracts.Hotel;
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;
using Turbo.Primitives.Plugins.Messages;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Watching;

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

    /// <summary>Players banned from it: they cannot watch it.</summary>
    public ISet<PlayerId> Banned { get; } = new HashSet<PlayerId>();

    /// <summary>A doorbell or a password keeps visitors out: they cannot watch it either.</summary>
    public bool DoorClosed { get; set; }

    /// <summary>Its floor furni, as <see cref="IHotelRooms.FurniAsync"/> answers them.</summary>
    public List<HotelRoomFurni> Furni { get; } = [];

    /// <summary>Stands a floor furni of the definition on a tile; item ids count up from 1 in each room.</summary>
    public HotelRoomFurni AddFurni(int definitionId, int x, int y)
    {
        var furni = new HotelRoomFurni(Furni.Count + 1, definitionId, x, y);

        Furni.Add(furni);

        return furni;
    }

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
    private readonly List<(RoomId Room, PlayerId Player, PlayerId By)> _kicks = [];
    private readonly List<(RoomId Room, int ItemId, FurniLogicMessage Message)> _furniMessages = [];
    private readonly Dictionary<(RoomId Room, string Holder), DateTimeOffset> _holds = [];
    private readonly Dictionary<(PlayerId Player, RoomId Room), FakeWatch> _watches = [];

    internal FakeRooms(FakeHotel hotel) => _hotel = hotel;

    public IReadOnlyList<FakeRoom> All => _rooms;

    public IReadOnlyList<(RoomId Room, object Message)> Sent => _sent;

    public IReadOnlyList<(RoomId Room, PlayerId Player, string Text)> Whispers => _whispers;

    /// <summary>Each kick made: the room, the player kicked and who kicked them.</summary>
    public IReadOnlyList<(RoomId Room, PlayerId Player, PlayerId By)> Kicks => _kicks;

    /// <summary>Every furni logic message handed to a room, in order.</summary>
    public IReadOnlyList<(RoomId Room, int ItemId, FurniLogicMessage Message)> FurniMessages =>
        _furniMessages;

    /// <summary>
    /// What a furni logic message is answered with; <see cref="FurniLogicReply.UnknownItem"/> when
    /// unset, as a room without that item answers.
    /// </summary>
    public Func<RoomId, int, FurniLogicMessage, FurniLogicReply>? FurniLogic { get; set; }

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

    public Task<IReadOnlyList<HotelRoomFurni>> FurniAsync(RoomId room, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<HotelRoomFurni>>(
            Find(room) is { Players.Count: > 0 } r ? [.. r.Furni] : []
        );

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

    /// <summary>
    /// Takes the player out of the room and notes the kick (<see cref="Kicks"/>); the room's owner
    /// stays, as no one outranks them in their own room. False when they are not in it.
    /// </summary>
    public Task<bool> KickAsync(RoomId room, PlayerId player, PlayerId by, CancellationToken ct)
    {
        if (Find(room) is not { } r || !r.Players.Contains(player) || r.Owner.Id == player)
            return Task.FromResult(false);

        r.Players.Remove(player);
        _kicks.Add((room, player, by));

        return Task.FromResult(true);
    }

    public Task<FurniLogicReply> InvokeFurniLogicAsync(
        RoomId room,
        int itemId,
        FurniLogicMessage message,
        CancellationToken ct
    )
    {
        _furniMessages.Add((room, itemId, message));

        return Task.FromResult(
            FurniLogic?.Invoke(room, itemId, message) ?? FurniLogicReply.UnknownItem
        );
    }

    public Task LoadAsync(RoomId room, CancellationToken ct)
    {
        if (Find(room) is { } r)
            r.Loaded = true;

        return Task.CompletedTask;
    }

    /// <summary>
    /// The running hotel's rules in memory: a lease within <see cref="RoomWatchLimits.Clamp"/>, at
    /// most <see cref="RoomWatchLimits.MAX_ROOMS_PER_PLUGIN"/> rooms (a fake hotel is one plugin's),
    /// and the lease runs out by the hotel's clock.
    /// </summary>
    public Task<RoomHoldOutcome> HoldAsync(
        RoomId room,
        string holder,
        TimeSpan lease,
        CancellationToken ct
    )
    {
        if (Find(room) is not { } r)
            return Task.FromResult(RoomHoldOutcome.Failed);

        if (!IsHeldByPlugin(room) && HeldRooms().Count >= RoomWatchLimits.MAX_ROOMS_PER_PLUGIN)
            return Task.FromResult(RoomHoldOutcome.PluginFull);

        r.Loaded = true;
        _holds[(room, holder)] = Now + RoomWatchLimits.Clamp(lease);

        return Task.FromResult(RoomHoldOutcome.Held);
    }

    public Task<bool> ReleaseAsync(RoomId room, string holder, CancellationToken ct) =>
        Task.FromResult(_holds.Remove((room, holder)));

    /// <summary>
    /// The running hotel's rules in memory: an online player not in the room, not banned from it
    /// and not kept out by its door (<see cref="FakeRoom.DoorClosed"/>), at most
    /// <see cref="RoomWatchLimits.MAX_WATCHES_PER_PLAYER"/> rooms, until the lease runs out. An
    /// envelope with no carried packet field is refused as the hotel refuses it.
    /// </summary>
    public Task<RoomWatchOutcome> WatchAsync(
        PlayerId player,
        RoomId room,
        IPluginMessage envelope,
        TimeSpan lease,
        CancellationToken ct
    )
    {
        PluginEnvelopeComposer.OuterOf(envelope);
        DropExpired();

        if (!_hotel.Players.IsOnline(player))
            return Task.FromResult(RoomWatchOutcome.NotOnline);

        if (Find(room) is not { } r)
            return Task.FromResult(RoomWatchOutcome.NotHeld);

        if (r.Players.Contains(player))
            return Task.FromResult(RoomWatchOutcome.InThatRoom);

        var renewing = _watches.ContainsKey((player, room));

        if (!renewing && Watching(player).Count >= RoomWatchLimits.MAX_WATCHES_PER_PLAYER)
            return Task.FromResult(RoomWatchOutcome.TooManyWatches);

        RoomWatchOutcome? refused =
            r.Banned.Contains(player) ? RoomWatchOutcome.Banned
            : r.DoorClosed ? RoomWatchOutcome.DoorClosed
            : null;

        if (refused is { } outcome)
        {
            _watches.Remove((player, room));

            return Task.FromResult(outcome);
        }

        if (!IsHeldByPlugin(room) && HeldRooms().Count >= RoomWatchLimits.MAX_ROOMS_PER_PLUGIN)
            return Task.FromResult(RoomWatchOutcome.NotHeld);

        r.Loaded = true;
        _watches[(player, room)] = new FakeWatch(envelope, Now + RoomWatchLimits.Clamp(lease));

        return Task.FromResult(RoomWatchOutcome.Watching);
    }

    public Task<bool> UnwatchAsync(PlayerId player, RoomId room, CancellationToken ct) =>
        Task.FromResult(_watches.Remove((player, room)));

    /// <summary>Whether a hold or a watch keeps the room loaded now.</summary>
    public bool IsHeld(RoomId room)
    {
        DropExpired();

        return IsHeldByPlugin(room);
    }

    /// <summary>The rooms the player watches now.</summary>
    public IReadOnlyList<RoomId> Watching(PlayerId player)
    {
        DropExpired();

        return [.. _watches.Keys.Where(x => x.Player == player).Select(x => x.Room).Order()];
    }

    /// <summary>The envelope the player's watch of the room wraps its packets in, or null.</summary>
    public IPluginMessage? WatchEnvelope(PlayerId player, RoomId room)
    {
        DropExpired();

        return _watches.TryGetValue((player, room), out var watch) ? watch.Envelope : null;
    }

    private DateTimeOffset Now => _hotel.Clock.GetUtcNow();

    private bool IsHeldByPlugin(RoomId room) =>
        _holds.Keys.Any(x => x.Room == room) || _watches.Keys.Any(x => x.Room == room);

    private HashSet<RoomId> HeldRooms()
    {
        DropExpired();

        return [.. _holds.Keys.Select(x => x.Room), .. _watches.Keys.Select(x => x.Room)];
    }

    private void DropExpired()
    {
        var now = Now;

        foreach (var (key, until) in _holds.ToList())
            if (until <= now)
                _holds.Remove(key);

        foreach (var (key, watch) in _watches.ToList())
            if (watch.Until <= now)
                _watches.Remove(key);
    }

    private sealed record FakeWatch(IPluginMessage Envelope, DateTimeOffset Until);

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
