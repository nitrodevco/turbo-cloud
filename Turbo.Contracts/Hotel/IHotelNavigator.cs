using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Rooms;

namespace Turbo.Contracts.Hotel;

/// <summary>The hotel's navigator as a plugin changes it.</summary>
public interface IHotelNavigator
{
    /// <summary>The Public tab: its categories and the rooms in them (<c>docs/hotel-api.md</c>).</summary>
    [HotelCall(HotelCallContext.Anywhere)]
    IHotelPublicRooms Public { get; }
}

/// <summary>
/// A category of the navigator's Public tab: one block of Habbo's <c>official_view</c>, titled
/// <see cref="Name"/> (a literal or a <c>${text.key}</c>), with its rooms in their order.
/// <see cref="Key"/> is the plugin's that made it, null for one staff made;
/// <see cref="StaffPicks"/> lists the staff-picked rooms, newest pick first, and takes no rooms.
/// </summary>
public sealed record HotelPublicCategory(
    int Id,
    string? Key,
    string Name,
    bool Visible,
    bool ForceClosed,
    bool Thumbnails,
    bool StaffPicks,
    IReadOnlyList<RoomId> Rooms
);

/// <summary>A category a plugin makes, as it starts out: shown, open, thumbnails and the rooms in this order unless said.</summary>
public sealed record HotelPublicCategoryDefaults(
    string Name,
    bool Visible = true,
    bool ForceClosed = false,
    bool Thumbnails = true,
    IReadOnlyList<RoomId>? Rooms = null
);

/// <summary>
/// The navigator's Public tab, changed through the same editor as the admin panel's Public rooms
/// page: players see a change with their next search (Habbo pushes none), and each change is in
/// the command log as the plugin's (source <c>plugin</c>). A plugin changes only the categories it
/// made, by their key (<c>&lt;plugin-key&gt;:&lt;name&gt;</c>, <see cref="HotelPublicKey"/>);
/// staff may rename, hide, reorder and refill those too, and a plugin never undoes it:
/// <see cref="EnsureCategoryAsync"/> makes a category once and leaves it as staff left it after.
/// A key the plugin made no category under, a room that does not exist or an order that does not
/// name every room once throws <see cref="ArgumentException"/>.
/// </summary>
public interface IHotelPublicRooms
{
    /// <summary>Every category in the tab's order, hidden ones too, each with its rooms.</summary>
    [HotelCall(HotelCallContext.OffGrain)]
    Task<IReadOnlyList<HotelPublicCategory>> CategoriesAsync(CancellationToken ct);

    /// <summary>
    /// The category under <paramref name="key"/>: made from <paramref name="defaults"/> (last in
    /// the tab) when there is none, else as it is now, staff's changes kept. Call it as the plugin
    /// starts, every time.
    /// </summary>
    [HotelCall(HotelCallContext.OffGrain)]
    Task<HotelPublicCategory> EnsureCategoryAsync(
        string key,
        HotelPublicCategoryDefaults defaults,
        CancellationToken ct
    );

    /// <summary>Puts the room in the category, first or last; false when it is in it already.</summary>
    [HotelCall(HotelCallContext.OffGrain)]
    Task<bool> AddRoomAsync(string key, RoomId room, bool first, CancellationToken ct);

    /// <summary>Takes the room out of the category; false when it was not in it.</summary>
    [HotelCall(HotelCallContext.OffGrain)]
    Task<bool> RemoveRoomAsync(string key, RoomId room, CancellationToken ct);

    /// <summary>Puts the category's rooms in this order: every room in it, each once.</summary>
    [HotelCall(HotelCallContext.OffGrain)]
    Task OrderRoomsAsync(string key, IReadOnlyList<RoomId> rooms, CancellationToken ct);
}

/// <summary>The rules of a Public tab category's key.</summary>
public static class HotelPublicKey
{
    /// <summary>The longest key, in characters.</summary>
    public const int MAX_LENGTH = 100;

    /// <summary>
    /// Throws an <see cref="ArgumentException"/> for a key that is not
    /// <c>&lt;plugin-key&gt;:&lt;name&gt;</c> (both parts set) or is longer than
    /// <see cref="MAX_LENGTH"/>: a mistake in the plugin.
    /// </summary>
    public static void ThrowIfInvalid(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(key.Length, MAX_LENGTH, nameof(key));

        var separator = key.IndexOf(':', StringComparison.Ordinal);

        if (separator <= 0 || separator == key.Length - 1 || key.Trim() != key)
            throw new ArgumentException(
                $"A public category key is <plugin-key>:<name>, not \"{key}\".",
                nameof(key)
            );
    }

    /// <summary>The plugin a valid key is of.</summary>
    public static string PluginOf(string key) => key[..key.IndexOf(':', StringComparison.Ordinal)];
}
