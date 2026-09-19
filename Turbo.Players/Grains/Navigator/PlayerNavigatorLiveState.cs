using System;
using System.Collections.Generic;
using Turbo.Primitives.Navigator.Enums;
using Turbo.Primitives.Navigator.Snapshots;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;

namespace Turbo.Players.Grains.Navigator;

internal sealed class PlayerNavigatorLiveState
{
    public required PlayerId PlayerId { get; init; }
    public List<RoomId> FavouriteRoomIds { get; } = [];
    public List<NavigatorQuickLinkSnapshot> SavedSearches { get; } = [];
    public HashSet<string> CollapsedSearchCodes { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, NavigatorViewModeType> ViewModes { get; } =
        new(StringComparer.Ordinal);
    public Dictionary<RoomId, RoomVisitStats> VisitsByRoomId { get; } = [];
    public List<RoomId> PendingVisits { get; } = [];

    /// <summary>Saved search ids are stored with each search, so the client sees the same id every login.</summary>
    public int NextSavedSearchId { get; set; } = 1;
    public int FailedVisitWrites { get; set; }
    public Queue<DateTime> UncachedSearchTimes { get; } = new();

    /// <summary>Preference changes bump the version; a flush that completes at that version is clean.</summary>
    public int PreferencesVersion { get; set; }
    public int PersistedPreferencesVersion { get; set; }
}
