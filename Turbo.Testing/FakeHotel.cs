using System;
using System.Collections.Generic;
using System.Linq;
using Turbo.Contracts.Hotel;
using Turbo.Primitives.Players;

namespace Turbo.Testing;

/// <summary>
/// The hotel API in memory, for a plugin's tests: players, rooms, wallets, inventories, a catalog
/// and notices the test sets up, the same rules as the running hotel's (turbo-cloud's contract
/// tests run one set of scenarios against both), and a record of everything the plugin did, with
/// assertions on it (<c>hotel.Wallet.AssertCredited(alice, "credits", 50)</c>). Not thread safe
/// beyond what one test does at a time. Its clock stands still until the test moves it.
/// </summary>
public sealed class FakeHotel : IHotel
{
    public FakeHotel(DateTimeOffset? now = null)
    {
        Clock = new ManualClock(now ?? new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
        Wallet = new FakeWallet(this);
        Players = new FakePlayers(this);
        Rooms = new FakeRooms(this);
        Inventory = new FakeInventory(this);
        Catalog = new FakeCatalog(this);
        Notify = new FakeNotify(this);
        Wired = new FakeWired(this);
    }

    public FakePlayers Players { get; }

    public FakeRooms Rooms { get; }

    public FakeWallet Wallet { get; }

    public FakeInventory Inventory { get; }

    public FakeCatalog Catalog { get; }

    public FakeNotify Notify { get; }

    public FakeWired Wired { get; }

    public ManualClock Clock { get; }

    IHotelPlayers IHotel.Players => Players;

    IHotelRooms IHotel.Rooms => Rooms;

    IHotelWallet IHotel.Wallet => Wallet;

    IHotelInventory IHotel.Inventory => Inventory;

    IHotelCatalog IHotel.Catalog => Catalog;

    IHotelNotify IHotel.Notify => Notify;

    IHotelWired IHotel.Wired => Wired;

    TimeProvider IHotel.Clock => Clock;

    internal FakePlayer Player(PlayerId id) =>
        Players.All.FirstOrDefault(x => x.Id == id)
        ?? throw new HotelAssertionException($"The fake hotel has no player {id}: add them first.");

    internal bool IsPlayer(PlayerId id) => Players.All.Any(x => x.Id == id);
}

/// <summary>A check on what a plugin did to a <see cref="FakeHotel"/> failed.</summary>
public sealed class HotelAssertionException(string message) : Exception(message);

/// <summary>A clock that moves only when told: <see cref="Advance"/> or <see cref="Set"/>.</summary>
public sealed class ManualClock(DateTimeOffset now) : TimeProvider
{
    private DateTimeOffset _now = now;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now += by;

    public void Set(DateTimeOffset now) => _now = now;
}

internal static class FakeHotelText
{
    public static string List<T>(IEnumerable<T> items) =>
        items.Any() ? string.Join(", ", items) : "nothing";
}
