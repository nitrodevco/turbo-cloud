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
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Testing;

/// <summary>A player of a <see cref="FakeHotel"/>; the test changes them as it likes. Passes as their <see cref="PlayerId"/>.</summary>
public sealed class FakePlayer
{
    internal FakePlayer(PlayerId id, string name, DateTime createdAt)
    {
        Id = id;
        Name = name;
        CreatedAt = createdAt;
    }

    public PlayerId Id { get; }

    public string Name { get; set; }

    public string Figure { get; set; } = "hd-180-1.ch-210-66.lg-270-82.sh-290-80";

    public AvatarGenderType Gender { get; set; } = AvatarGenderType.Male;

    public string Motto { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime? LastLoginAt { get; set; }

    public bool Online { get; set; }

    /// <summary>The hotel's own accounts (bots, staff accounts marked so): never on a toplist, nor in circulation.</summary>
    public bool IsSystemAccount { get; set; }

    public int Respects { get; set; }

    /// <summary>The respects the player gave other players.</summary>
    public int RespectsGiven { get; set; }

    /// <summary>The player's time online before the session running now.</summary>
    public TimeSpan OnlineTime { get; set; }

    /// <summary>When the session running now began; null when there is none (<see cref="Online"/> alone starts none).</summary>
    public DateTime? SessionStartedAt { get; set; }

    public long AchievementScore { get; set; }

    public HotelClub Club { get; set; }

    /// <summary>The permission nodes the player holds; <c>*</c> holds them all.</summary>
    public ISet<string> Permissions { get; } = new HashSet<string>(StringComparer.Ordinal);

    public static implicit operator PlayerId(FakePlayer player) => player.Id;

    public override string ToString() => $"{Name} ({Id})";
}

/// <summary>The players of a <see cref="FakeHotel"/>, and what was sent to them.</summary>
public sealed class FakePlayers : IHotelPlayers
{
    private readonly FakeHotel _hotel;
    private readonly List<FakePlayer> _players = [];
    private readonly List<(PlayerId Player, object Message)> _sent = [];
    private readonly Dictionary<(string Provider, string Id), PlayerId> _identities = [];

    internal FakePlayers(FakeHotel hotel) => _hotel = hotel;

    public IReadOnlyList<FakePlayer> All => _players;

    /// <summary>
    /// The id the next <see cref="Add"/> gets (1 to start); set it when the code under test keeps
    /// ids of its own (a system account at 1, ids from a recorded hotel).
    /// </summary>
    public int NextId { get; set; } = 1;

    /// <summary>Every message and composer sent to a player who was online, in order.</summary>
    public IReadOnlyList<(PlayerId Player, object Message)> Sent => _sent;

    /// <summary>A new player, ids counting up from <see cref="NextId"/>.</summary>
    public FakePlayer Add(string name, bool online = false)
    {
        if (_players.Any(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException($"The fake hotel already has a player called {name}.");

        var player = new FakePlayer(
            new PlayerId(TakeId(name)),
            name,
            _hotel.Clock.GetUtcNow().UtcDateTime
        )
        {
            Online = online,
        };

        _players.Add(player);

        return player;
    }

    private int TakeId(string name)
    {
        if (_players.Any(x => x.Id.Value == NextId))
            throw new ArgumentException(
                $"The fake hotel already has a player {NextId}; {name} needs another NextId."
            );

        return NextId++;
    }

    public Task<HotelPlayer?> GetAsync(PlayerId player, CancellationToken ct) =>
        Task.FromResult(_players.FirstOrDefault(x => x.Id == player) is { } p ? ToPlayer(p) : null);

    public Task<HotelPlayer?> FindAsync(string name, CancellationToken ct)
    {
        var key = PlayerNames.Key(name ?? string.Empty);

        return Task.FromResult(
            key.Length > 0 && _players.FirstOrDefault(x => PlayerNames.Key(x.Name) == key) is { } p
                ? ToPlayer(p)
                : null
        );
    }

    public Task<IReadOnlyDictionary<PlayerId, string>> NamesAsync(
        IReadOnlyCollection<PlayerId> players,
        CancellationToken ct
    ) =>
        Task.FromResult<IReadOnlyDictionary<PlayerId, string>>(
            _players.Where(x => players.Contains(x.Id)).ToDictionary(x => x.Id, x => x.Name)
        );

    public Task<IReadOnlyList<HotelPlayer>> SearchAsync(
        string prefix,
        int take,
        CancellationToken ct
    )
    {
        var key = PlayerNames.Key(prefix ?? string.Empty);
        take = Math.Clamp(take, 0, 50);

        if (key.Length == 0)
            return Task.FromResult<IReadOnlyList<HotelPlayer>>([]);

        return Task.FromResult<IReadOnlyList<HotelPlayer>>([
            .. _players
                .Where(x => PlayerNames.Key(x.Name).StartsWith(key, StringComparison.Ordinal))
                .OrderBy(x => PlayerNames.Key(x.Name), StringComparer.Ordinal)
                .ThenBy(x => x.Id.Value)
                .Take(take)
                .Select(ToPlayer),
        ]);
    }

    public Task<int> CountAsync(DateTime? activeSince, CancellationToken ct) =>
        Task.FromResult(Active(activeSince).Count());

    public Task<IReadOnlyList<HotelRecentPlayer>> RecentAsync(
        DateTime activeSince,
        int take,
        int skip,
        CancellationToken ct
    ) =>
        Task.FromResult<IReadOnlyList<HotelRecentPlayer>>([
            .. Active(activeSince)
                .OrderByDescending(x => x.LastLoginAt)
                .ThenBy(x => x.Id.Value)
                .Skip(Math.Max(0, skip))
                .Take(Math.Clamp(take, 0, 100))
                .Select(x => new HotelRecentPlayer(ToPlayer(x), x.Online)),
        ]);

    /// <summary>Every identity linked to a player, by service and id there.</summary>
    public IReadOnlyDictionary<(string Provider, string Id), PlayerId> Identities => _identities;

    public Task<PlayerId?> FindByIdentityAsync(string provider, string id, CancellationToken ct) =>
        Task.FromResult<PlayerId?>(
            IsDiscord(provider) && _identities.TryGetValue((HotelIdentity.DISCORD, id), out var p)
                ? p
                : null
        );

    public Task<IdentityLinkOutcome> LinkIdentityAsync(
        PlayerId player,
        HotelIdentity identity,
        bool replace,
        CancellationToken ct
    )
    {
        if (
            !IsDiscord(identity.Provider)
            || string.IsNullOrEmpty(identity.Id)
            || identity.Id.Length > 32
        )
            return Task.FromResult(IdentityLinkOutcome.Unsupported);

        if (_players.All(x => x.Id != player))
            return Task.FromResult(IdentityLinkOutcome.NoSuchPlayer);

        var key = (HotelIdentity.DISCORD, identity.Id);
        var held = _identities.TryGetValue(key, out var holder) ? holder : (PlayerId?)null;
        var own = _identities.Where(x => x.Value == player).Select(x => x.Key).ToList();

        if (held == player)
            return Task.FromResult(IdentityLinkOutcome.AlreadyLinked);

        if (!replace && held is not null)
            return Task.FromResult(IdentityLinkOutcome.TakenByAnother);

        if (!replace && own.Count > 0)
            return Task.FromResult(IdentityLinkOutcome.PlayerHasAnother);

        foreach (var old in own)
            _identities.Remove(old);

        _identities[key] = player;

        return Task.FromResult(IdentityLinkOutcome.Linked);
    }

    public Task<bool> UnlinkIdentityAsync(PlayerId player, string provider, CancellationToken ct)
    {
        if (!IsDiscord(provider))
            return Task.FromResult(false);

        var own = _identities.Where(x => x.Value == player).Select(x => x.Key).ToList();

        foreach (var old in own)
            _identities.Remove(old);

        return Task.FromResult(own.Count > 0);
    }

    public IReadOnlyCollection<PlayerId> OnlineIds() =>
        [.. _players.Where(x => x.Online).Select(x => x.Id)];

    public Task<RoomId?> CurrentRoomAsync(PlayerId player, CancellationToken ct) =>
        Task.FromResult(_hotel.Rooms.RoomOf(player));

    public Task<HotelClub> ClubAsync(PlayerId player, CancellationToken ct) =>
        Task.FromResult(_players.FirstOrDefault(x => x.Id == player)?.Club ?? default);

    public Task<HotelOnlineTime?> OnlineTimeAsync(PlayerId player, CancellationToken ct)
    {
        if (_players.FirstOrDefault(x => x.Id == player) is not { } p)
            return Task.FromResult<HotelOnlineTime?>(null);

        var now = _hotel.Clock.GetUtcNow().UtcDateTime;
        var session =
            p.SessionStartedAt is { } started && now > started ? now - started : TimeSpan.Zero;

        return Task.FromResult<HotelOnlineTime?>(new(p.OnlineTime + session, session));
    }

    public Task<HotelRespects?> RespectsAsync(PlayerId player, CancellationToken ct) =>
        Task.FromResult<HotelRespects?>(
            _players.FirstOrDefault(x => x.Id == player) is { } p
                ? new HotelRespects(p.Respects, p.RespectsGiven)
                : null
        );

    public Task<bool> HasPermissionAsync(PlayerId player, string node, CancellationToken ct) =>
        Task.FromResult(
            _players.FirstOrDefault(x => x.Id == player) is { } p
                && (p.Permissions.Contains(node) || p.Permissions.Contains("*"))
        );

    public Task<IReadOnlyList<HotelRanked>> TopByAsync(
        PlayerStat stat,
        int take,
        int skip,
        CancellationToken ct
    )
    {
        take = Math.Clamp(take, 0, 100);
        skip = Math.Max(0, skip);

        var ranked = _players.Where(x => !x.IsSystemAccount);
        IEnumerable<(FakePlayer Player, long Value)> figures;

        if (stat == PlayerStat.Respects)
            figures = ranked.Select(x => (x, (long)x.Respects));
        else if (stat == PlayerStat.AchievementScore)
            figures = ranked
                .Where(x => x.AchievementScore != 0)
                .Select(x => (x, x.AchievementScore));
        else if (stat.Currency is { } currency && _hotel.Wallet.CurrencyExists(currency))
            figures = ranked
                .Where(x => _hotel.Wallet.Holds(x.Id, currency))
                .Select(x => (x, (long)_hotel.Wallet.Balance(x.Id, currency)));
        else
            figures = [];

        return Task.FromResult<IReadOnlyList<HotelRanked>>([
            .. figures
                .OrderByDescending(x => x.Value)
                .ThenBy(x => x.Player.Id.Value)
                .Skip(skip)
                .Take(take)
                .Select(
                    (x, i) => new HotelRanked(skip + i + 1, x.Player.Id, x.Player.Name, x.Value)
                ),
        ]);
    }

    public Task<bool> SendAsync(PlayerId player, IPluginMessage message, CancellationToken ct) =>
        Task.FromResult(Record(player, message));

    public Task<bool> SendAsync(PlayerId player, IComposer composer, CancellationToken ct) =>
        Task.FromResult(Record(player, composer));

    /// <summary>What was sent to the player, in order.</summary>
    public IReadOnlyList<object> SentTo(PlayerId player) =>
        [.. _sent.Where(x => x.Player == player).Select(x => x.Message)];

    /// <summary>The last message or composer of type <typeparamref name="T"/> sent to the player; fails when there is none.</summary>
    public T AssertSent<T>(PlayerId player)
        where T : class =>
        SentTo(player).OfType<T>().LastOrDefault()
        ?? throw new HotelAssertionException(
            $"Expected a {typeof(T).Name} sent to player {player}; they were sent {FakeHotelText.List(SentTo(player).Select(x => x.GetType().Name))}."
        );

    public void AssertNothingSent(PlayerId player)
    {
        if (SentTo(player).Count > 0)
            throw new HotelAssertionException(
                $"Expected nothing sent to player {player}; they were sent {FakeHotelText.List(SentTo(player).Select(x => x.GetType().Name))}."
            );
    }

    internal bool IsOnline(PlayerId player) =>
        _players.FirstOrDefault(x => x.Id == player)?.Online ?? false;

    private bool Record(PlayerId player, object message)
    {
        if (!IsOnline(player))
            return false;

        _sent.Add((player, message));

        return true;
    }

    // As the host: not system accounts, and about since then (logged in or online now).
    private IEnumerable<FakePlayer> Active(DateTime? since) =>
        _players.Where(x =>
            !x.IsSystemAccount && (since is not { } from || x.LastLoginAt >= from || x.Online)
        );

    private static bool IsDiscord(string provider) =>
        string.Equals(provider, HotelIdentity.DISCORD, StringComparison.OrdinalIgnoreCase);

    private static HotelPlayer ToPlayer(FakePlayer x) =>
        new(x.Id, x.Name, x.Figure, x.Gender, x.Motto, x.CreatedAt, x.LastLoginAt);
}
