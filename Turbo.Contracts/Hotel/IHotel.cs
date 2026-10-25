using System;

namespace Turbo.Contracts.Hotel;

/// <summary>
/// The running hotel as a plugin uses it: its players, rooms, wallets, inventories, catalog,
/// notices and clock, without a grain, a session or a table. Take it by constructor in any plugin
/// class (a command, a handler, a route, a service of the plugin's own) and test against
/// <c>Turbo.Testing.FakeHotel</c>, which keeps the same rules in memory (<c>docs/hotel-api.md</c>).
/// Every member says where it may be called from (<see cref="HotelCallAttribute"/>).
/// </summary>
public interface IHotel
{
    IHotelPlayers Players { get; }

    IHotelRooms Rooms { get; }

    IHotelWallet Wallet { get; }

    IHotelInventory Inventory { get; }

    IHotelCatalog Catalog { get; }

    IHotelNotify Notify { get; }

    /// <summary>The hotel's time: what its grains read. A test moves it by hand.</summary>
    TimeProvider Clock { get; }
}

/// <summary>Where a member of the hotel API may be called from.</summary>
public enum HotelCallContext
{
    /// <summary>In memory: never waits on a grain or the database, so any context, a room's turn included.</summary>
    Anywhere,

    /// <summary>
    /// Asks the grain of the player or room it is given. Call it from a handler, a command, a web
    /// route, a task or another grain, but never await it inside the turn of the grain it asks:
    /// a room listener or a room's command awaiting its own room waits on itself.
    /// </summary>
    NotFromItsGrain,

    /// <summary>
    /// Reads the database (or waits on other plugins). Call it from a handler, a web route, an admin page or a task, never
    /// inside a room's turn (a room listener, a command, furni logic): the room would wait for the
    /// database. Copy what you need out of the turn and read it after.
    /// </summary>
    OffGrain,
}

/// <summary>Where the member it marks may be called from (<see cref="HotelCallContext"/>).</summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Property)]
public sealed class HotelCallAttribute(HotelCallContext context) : Attribute
{
    public HotelCallContext Context { get; } = context;
}
