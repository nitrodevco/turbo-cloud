using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Navigator.Enums;
using Turbo.Primitives.Navigator.Snapshots;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Primitives.Navigator;

public interface INavigatorService
{
    public Task<ImmutableArray<NavigatorTopLevelContextSnapshot>> GetTopLevelContextAsync();

    /// <summary>
    /// Answers a new-navigator search: a top-level context (<c>hotel_view</c>, <c>myworld_view</c>,
    /// ...) returns one preview block per section, a section code returns that section in full,
    /// and a non-empty <paramref name="filter"/> returns a single block of text-search results.
    /// </summary>
    public Task<ImmutableArray<NavigatorSearchResultBlockSnapshot>> SearchAsync(
        PlayerId playerId,
        string searchCode,
        string filter,
        CancellationToken ct
    );

    /// <summary>Answers the legacy navigator searches.</summary>
    public Task<ImmutableArray<NavigatorSearchResultSnapshot>> SearchRoomsAsync(
        PlayerId playerId,
        NavigatorSearchType searchType,
        string searchParam,
        CancellationToken ct
    );

    public ImmutableArray<NavigatorFlatCategorySnapshot> GetFlatCategoriesForPlayer(
        PlayerId playerId
    );
    public ImmutableArray<NavigatorEventCategorySnapshot> GetEventCategories();
    public Task<ImmutableArray<NavigatorPopularTagSnapshot>> GetPopularTagsAsync(
        CancellationToken ct
    );
    public Task<ImmutableArray<NavigatorSearchResultSnapshot>> GetOfficialRoomsAsync(
        CancellationToken ct
    );
    public Task<(bool CanCreate, int RoomLimit)> CanCreateRoomAsync(
        PlayerId playerId,
        CancellationToken ct
    );

    /// <summary>Validates and creates a room; returns null when the request is rejected.</summary>
    public Task<RoomId?> CreateRoomAsync(
        PlayerId playerId,
        string name,
        string description,
        string modelName,
        int categoryId,
        int playersMax,
        RoomTradeModeType tradeType,
        CancellationToken ct
    );

    public Task<RoomId?> GetRandomPromotedRoomAsync(string eventCategory, CancellationToken ct);

    public bool CanManageStaffPicks(PlayerId playerId);

    /// <summary>Whether the room exists, answered from the room cache.</summary>
    public Task<bool> RoomExistsAsync(RoomId roomId, CancellationToken ct);
    public Task AddFavouriteRoomAsync(PlayerId playerId, RoomId roomId, CancellationToken ct);
    public Task RemoveFavouriteRoomAsync(PlayerId playerId, RoomId roomId, CancellationToken ct);
    public Task<ImmutableArray<RoomId>> GetFavouriteRoomIdsAsync(
        PlayerId playerId,
        CancellationToken ct
    );
    public int FavouriteRoomLimit { get; }
    public Task AddSavedSearchAsync(
        PlayerId playerId,
        string searchCode,
        string filter,
        CancellationToken ct
    );
    public Task AddCollapsedSearchCodeAsync(
        PlayerId playerId,
        string searchCode,
        CancellationToken ct
    );
    public Task SetViewModeAsync(
        PlayerId playerId,
        string searchCode,
        NavigatorViewModeType viewMode,
        CancellationToken ct
    );

    /// <summary>Longest search code or filter accepted from the client.</summary>
    public int MaxSearchCodeLength { get; }
    public int MaxTagsPerRoom { get; }
    public int MaxTagLength { get; }
    public TimeSpan RoomEventDuration { get; }
}
