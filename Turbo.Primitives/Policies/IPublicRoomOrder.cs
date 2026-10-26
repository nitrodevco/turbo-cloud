using System.Collections.Generic;
using Turbo.Primitives.Navigator.Enums;
using Turbo.Primitives.Rooms.Snapshots;

namespace Turbo.Primitives.Policies;

/// <summary>
/// A policy over the order of a Public tab category's rooms (<c>official_view</c>), asked each time
/// a block is built: a plugin's own order (busiest first, newest first) in place of the order staff
/// set. Not Habbo's: Habbo's Public tab lists its rooms in the order staff set (official client
/// capture 2026-10-18, seven samples), and so does the hotel with no such policy. A public class
/// implementing it is found in a plugin's assembly as it loads and taken away as it unloads; see
/// <see cref="IGamePolicies"/> for the order several run in.
/// <para>
/// It runs inside a navigator search, so it answers at once from the rooms it is given, never
/// from the database or another grain. Only the order counts: rooms it leaves out are put back
/// after the ones it named, in their order before, and rooms it adds are ignored. A policy that
/// throws is logged and passed over.
/// </para>
/// </summary>
public interface IPublicRoomOrder
{
    /// <summary>Lower runs first, each on the order the one before left. 0 by default.</summary>
    int Order => 0;

    /// <param name="category">The category the block is of.</param>
    /// <param name="rooms">
    /// Its rooms in the order so far (the curated one at the start), each with the players in it
    /// now as <see cref="RoomSummarySnapshot.Population"/>.
    /// </param>
    /// <returns>The same rooms in the order to list them.</returns>
    IReadOnlyList<RoomInfoSnapshot> OrderRooms(
        PublicRoomCategory category,
        IReadOnlyList<RoomInfoSnapshot> rooms
    );
}

/// <summary>
/// The Public tab category a block is of: its id, title, kind and the key of the plugin that made
/// it (<c>IHotelPublicRooms</c>), null for one staff made.
/// </summary>
public readonly record struct PublicRoomCategory(
    int Id,
    string Name,
    NavigatorPublicCategoryKind Kind,
    string? PluginKey
);
