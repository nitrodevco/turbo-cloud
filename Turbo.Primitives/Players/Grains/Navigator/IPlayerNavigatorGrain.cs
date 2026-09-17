using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Navigator.Enums;
using Turbo.Primitives.Players.Snapshots.Navigator;
using Turbo.Primitives.Rooms;

namespace Turbo.Primitives.Players.Grains.Navigator;

/// <summary>
/// Owns a player's navigator state: favourite rooms, saved searches, collapsed result blocks,
/// per-search view modes and room visit history. Mutations the client waits on send their own
/// confirmation composers to the player.
/// </summary>
public interface IPlayerNavigatorGrain : IGrainWithIntegerKey
{
    public Task<PlayerNavigatorSnapshot> GetSnapshotAsync(CancellationToken ct);
    public Task AddFavouriteRoomAsync(RoomId roomId, int limit, CancellationToken ct);
    public Task RemoveFavouriteRoomAsync(RoomId roomId, CancellationToken ct);
    public Task AddSavedSearchAsync(
        string searchCode,
        string filter,
        int limit,
        CancellationToken ct
    );
    public Task DeleteSavedSearchAsync(int savedSearchId, CancellationToken ct);
    public Task AddCollapsedSearchCodeAsync(string searchCode, int limit, CancellationToken ct);
    public Task RemoveCollapsedSearchCodeAsync(string searchCode, CancellationToken ct);
    public Task SetViewModeAsync(
        string searchCode,
        NavigatorViewModeType viewMode,
        int limit,
        CancellationToken ct
    );
    public Task RecordRoomVisitAsync(RoomId roomId, CancellationToken ct);

    /// <summary>
    /// Takes one uncached search from the player's quota of <paramref name="limit"/> per
    /// <paramref name="window"/>; false when the quota is used up.
    /// </summary>
    public Task<bool> TryConsumeSearchQuotaAsync(int limit, TimeSpan window, CancellationToken ct);
    public Task<ImmutableArray<RoomId>> GetRecentRoomIdsAsync(int limit, CancellationToken ct);
    public Task<ImmutableArray<RoomId>> GetFrequentRoomIdsAsync(int limit, CancellationToken ct);
}
