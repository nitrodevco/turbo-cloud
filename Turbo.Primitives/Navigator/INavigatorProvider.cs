using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Navigator.Enums;
using Turbo.Primitives.Navigator.Snapshots;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Snapshots;

namespace Turbo.Primitives.Navigator;

/// <summary>
/// Navigator data access: cached category/context tables plus room queries. Room results carry
/// persisted data only; live population is merged in by <see cref="INavigatorService"/>.
/// </summary>
public interface INavigatorProvider
{
    public Task<ImmutableArray<NavigatorTopLevelContextSnapshot>> GetTopLevelContextsAsync();
    public ImmutableArray<NavigatorFlatCategorySnapshot> GetFlatCategories();
    public ImmutableArray<NavigatorEventCategorySnapshot> GetEventCategories();
    public Task ReloadAsync(CancellationToken ct = default);

    /// <summary>The room directory position this silo's cache is current with.</summary>
    public (Guid Epoch, long Sequence) ListingPosition { get; }

    /// <summary>Evicts the listings the room directory reports as changed.</summary>
    public void ApplyListingChanges(RoomListingViewSnapshot view);

    public bool IsSearchCached(NavigatorSearchFilterType filterType, string value);

    public Task<List<RoomInfoSnapshot>> GetRoomsByIdsAsync(
        IReadOnlyCollection<RoomId> roomIds,
        CancellationToken ct
    );
    public Task<List<RoomInfoSnapshot>> GetRoomsByOwnersAsync(
        IReadOnlyCollection<PlayerId> ownerIds,
        int limit,
        CancellationToken ct
    );
    public Task<List<RoomInfoSnapshot>> GetRoomsWithRightsAsync(
        PlayerId playerId,
        int limit,
        CancellationToken ct
    );
    public Task<List<RoomInfoSnapshot>> GetRoomsByCategoryAsync(
        int categoryId,
        int limit,
        CancellationToken ct
    );
    public Task<List<RoomInfoSnapshot>> GetHighestScoredRoomsAsync(int limit, CancellationToken ct);
    public Task<List<RoomInfoSnapshot>> GetStaffPickedRoomsAsync(int limit, CancellationToken ct);
    public Task<List<RoomInfoSnapshot>> GetRoomsWithActiveEventsAsync(
        int? eventCategoryId,
        int limit,
        CancellationToken ct
    );
    public Task<List<RoomInfoSnapshot>> SearchRoomsAsync(
        NavigatorSearchFilterType filterType,
        string value,
        int limit,
        CancellationToken ct
    );
    public Task<List<(RoomId RoomId, ImmutableArray<string> Tags)>> GetRoomTagsAsync(
        CancellationToken ct
    );
    public Task<int> GetRoomCountForOwnerAsync(PlayerId ownerId, CancellationToken ct);
    public Task<RoomId> CreateRoomAsync(
        PlayerId ownerId,
        string name,
        string description,
        int modelId,
        int? categoryId,
        int playersMax,
        RoomTradeModeType tradeType,
        CancellationToken ct
    );
    public Task<int?> GetRoomModelIdByNameAsync(string modelName, CancellationToken ct);
}
