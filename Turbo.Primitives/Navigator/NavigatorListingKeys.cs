using System.Globalization;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;

namespace Turbo.Primitives.Navigator;

/// <summary>
/// Names of the cached room listings the navigator keeps. Whoever changes persisted room data
/// publishes the affected keys to the room directory, and every silo evicts them before its next
/// read, so cached listings never show a change late.
/// </summary>
public static class NavigatorListingKeys
{
    public const string HIGHEST_SCORED = "highest";
    public const string STAFF_PICKS = "staffpicks";
    public const string TAGS = "tags";

    /// <summary>Every event listing (all events and each event category).</summary>
    public const string EVENTS = "events";

    /// <summary>Every cached text search.</summary>
    public const string SEARCH = "search";

    public static string Room(RoomId roomId) => $"room:{Format(roomId.Value)}";

    public static string Owner(PlayerId ownerId) => $"owner:{Format(ownerId.Value)}";

    public static string OwnerRoomCount(PlayerId ownerId) => $"ownercount:{Format(ownerId.Value)}";

    public static string Category(int categoryId) => $"category:{Format(categoryId)}";

    public static string Rights(PlayerId playerId) => $"rights:{Format(playerId.Value)}";

    private static string Format(int value) => value.ToString(CultureInfo.InvariantCulture);
}
