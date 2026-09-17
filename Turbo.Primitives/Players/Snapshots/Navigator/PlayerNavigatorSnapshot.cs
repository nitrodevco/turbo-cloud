using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Navigator.Enums;
using Turbo.Primitives.Navigator.Snapshots;
using Turbo.Primitives.Rooms;

namespace Turbo.Primitives.Players.Snapshots.Navigator;

[GenerateSerializer, Immutable]
public sealed record PlayerNavigatorSnapshot
{
    [Id(0)]
    public required ImmutableArray<RoomId> FavouriteRoomIds { get; init; }

    [Id(1)]
    public required ImmutableArray<NavigatorQuickLinkSnapshot> SavedSearches { get; init; }

    [Id(2)]
    public required ImmutableHashSet<string> CollapsedSearchCodes { get; init; }

    [Id(3)]
    public required ImmutableDictionary<string, NavigatorViewModeType> ViewModes { get; init; }

    public NavigatorViewModeType GetViewMode(string searchCode) =>
        ViewModes.TryGetValue(searchCode, out var mode) ? mode : NavigatorViewModeType.Rows;
}
