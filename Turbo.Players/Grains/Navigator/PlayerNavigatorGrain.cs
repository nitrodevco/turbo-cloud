using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Database.Context;
using Turbo.Database.Entities.Players;
using Turbo.Database.Entities.Room;
using Turbo.Players.Configuration;
using Turbo.Primitives.Messages.Outgoing.Navigator;
using Turbo.Primitives.Messages.Outgoing.NewNavigator;
using Turbo.Primitives.Navigator.Enums;
using Turbo.Primitives.Navigator.Snapshots;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players.Grains.Navigator;
using Turbo.Primitives.Players.Snapshots.Navigator;
using Turbo.Primitives.Rooms;

namespace Turbo.Players.Grains.Navigator;

/// <summary>
/// Owns a player's navigator state. Everything is hydrated once and served from memory; changes
/// apply in memory immediately and are written on a timer and on deactivation, so rapid clicks
/// (toggling favourites, collapsing blocks, walking between rooms) never become a write each.
/// </summary>
internal sealed class PlayerNavigatorGrain : Grain, IPlayerNavigatorGrain
{
    private readonly IDbContextFactory<TurboDbContext> _dbCtxFactory;
    private readonly PlayerConfig _playerConfig;
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<IPlayerNavigatorGrain> _logger;

    private readonly PlayerNavigatorLiveState _state;

    private IDisposable? _flushTimer;

    public PlayerNavigatorGrain(
        IDbContextFactory<TurboDbContext> dbCtxFactory,
        IOptions<PlayerConfig> playerConfig,
        IGrainFactory grainFactory,
        ILogger<IPlayerNavigatorGrain> logger
    )
    {
        _dbCtxFactory = dbCtxFactory;
        _playerConfig = playerConfig.Value;
        _grainFactory = grainFactory;
        _logger = logger;

        _state = new() { PlayerId = this.GetPlayerId() };
    }

    public override async Task OnActivateAsync(CancellationToken ct)
    {
        try
        {
            await HydrateAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to hydrate navigator state for player {PlayerId}",
                _state.PlayerId
            );

            throw;
        }

        _flushTimer = this.RegisterGrainTimer<object?>(
            static async (self, ct) => await ((PlayerNavigatorGrain)self!).FlushAsync(ct),
            this,
            TimeSpan.FromMilliseconds(_playerConfig.NavigatorFlushMs),
            TimeSpan.FromMilliseconds(_playerConfig.NavigatorFlushMs)
        );
    }

    public override async Task OnDeactivateAsync(DeactivationReason reason, CancellationToken ct)
    {
        _flushTimer?.Dispose();
        _flushTimer = null;

        await FlushAsync(ct);
    }

    public Task<PlayerNavigatorSnapshot> GetSnapshotAsync(CancellationToken ct) =>
        Task.FromResult(
            new PlayerNavigatorSnapshot
            {
                FavouriteRoomIds = [.. _state.FavouriteRoomIds],
                SavedSearches = [.. _state.SavedSearches],
                CollapsedSearchCodes = [.. _state.CollapsedSearchCodes],
                ViewModes = _state.ViewModes.ToImmutableDictionary(StringComparer.Ordinal),
            }
        );

    public async Task AddFavouriteRoomAsync(RoomId roomId, int limit, CancellationToken ct)
    {
        if (roomId.Value <= 0 || _state.FavouriteRoomIds.Contains(roomId))
            return;

        if (_state.FavouriteRoomIds.Count >= limit)
        {
            _logger.LogWarning(
                "Rejected favourite room {RoomId} for player {PlayerId}: limit {Limit} reached",
                roomId,
                _state.PlayerId,
                limit
            );

            return;
        }

        _state.FavouriteRoomIds.Add(roomId);
        _state.PreferencesVersion++;

        await _grainFactory.SendComposerToPlayerAsync(
            _state.PlayerId,
            new FavouriteChangedMessageComposer { RoomId = roomId, Added = true },
            ct
        );
    }

    public async Task RemoveFavouriteRoomAsync(RoomId roomId, CancellationToken ct)
    {
        if (!_state.FavouriteRoomIds.Remove(roomId))
            return;

        _state.PreferencesVersion++;

        await _grainFactory.SendComposerToPlayerAsync(
            _state.PlayerId,
            new FavouriteChangedMessageComposer { RoomId = roomId, Added = false },
            ct
        );
    }

    public async Task AddSavedSearchAsync(
        string searchCode,
        string filter,
        int limit,
        CancellationToken ct
    )
    {
        searchCode = searchCode?.Trim() ?? string.Empty;
        filter = filter?.Trim() ?? string.Empty;

        if (
            searchCode.Length == 0
            || searchCode.Length > PlayerNavigatorSavedSearchEntity.SEARCH_CODE_MAX_LENGTH
            || filter.Length > PlayerNavigatorSavedSearchEntity.FILTER_MAX_LENGTH
        )
            return;

        if (_state.SavedSearches.Any(x => x.SearchCode == searchCode && x.Filter == filter))
            return;

        if (_state.SavedSearches.Count >= limit)
        {
            _logger.LogWarning(
                "Rejected saved search for player {PlayerId}: limit {Limit} reached",
                _state.PlayerId,
                limit
            );

            return;
        }

        _state.SavedSearches.Add(CreateSavedSearch(_state.NextSavedSearchId++, searchCode, filter));
        _state.PreferencesVersion++;

        await SendSavedSearchesAsync(ct);
    }

    public async Task DeleteSavedSearchAsync(int savedSearchId, CancellationToken ct)
    {
        if (_state.SavedSearches.RemoveAll(x => x.Id == savedSearchId) == 0)
            return;

        _state.PreferencesVersion++;

        await SendSavedSearchesAsync(ct);
    }

    public Task AddCollapsedSearchCodeAsync(string searchCode, int limit, CancellationToken ct)
    {
        if (
            IsValidSearchCode(searchCode)
            && _state.CollapsedSearchCodes.Count < limit
            && _state.CollapsedSearchCodes.Add(searchCode)
        )
            _state.PreferencesVersion++;

        return Task.CompletedTask;
    }

    public Task RemoveCollapsedSearchCodeAsync(string searchCode, CancellationToken ct)
    {
        if (_state.CollapsedSearchCodes.Remove(searchCode))
            _state.PreferencesVersion++;

        return Task.CompletedTask;
    }

    public Task SetViewModeAsync(
        string searchCode,
        NavigatorViewModeType viewMode,
        int limit,
        CancellationToken ct
    )
    {
        if (!IsValidSearchCode(searchCode) || !Enum.IsDefined(viewMode))
            return Task.CompletedTask;

        var exists = _state.ViewModes.TryGetValue(searchCode, out var current);

        if ((exists && current == viewMode) || (!exists && _state.ViewModes.Count >= limit))
            return Task.CompletedTask;

        _state.ViewModes[searchCode] = viewMode;
        _state.PreferencesVersion++;

        return Task.CompletedTask;
    }

    public Task RecordRoomVisitAsync(RoomId roomId, CancellationToken ct)
    {
        if (roomId.Value <= 0)
            return Task.CompletedTask;

        var now = DateTime.UtcNow;

        _state.VisitsByRoomId[roomId] = _state.VisitsByRoomId.TryGetValue(roomId, out var stats)
            ? new RoomVisitStats(stats.Visits + 1, now)
            : new RoomVisitStats(1, now);

        TrimVisitHistory();

        if (_state.PendingVisits.Count >= _playerConfig.NavigatorMaxPendingVisits)
            _state.PendingVisits.RemoveAt(0);

        _state.PendingVisits.Add(roomId);

        return Task.CompletedTask;
    }

    public Task<bool> TryConsumeSearchQuotaAsync(int limit, TimeSpan window, CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        while (
            _state.UncachedSearchTimes.Count > 0
            && now - _state.UncachedSearchTimes.Peek() >= window
        )
            _state.UncachedSearchTimes.Dequeue();

        if (_state.UncachedSearchTimes.Count >= limit)
            return Task.FromResult(false);

        _state.UncachedSearchTimes.Enqueue(now);

        return Task.FromResult(true);
    }

    public Task<ImmutableArray<RoomId>> GetRecentRoomIdsAsync(int limit, CancellationToken ct) =>
        Task.FromResult<ImmutableArray<RoomId>>([
            .. _state
                .VisitsByRoomId.OrderByDescending(x => x.Value.LastVisitUtc)
                .Take(limit)
                .Select(x => x.Key),
        ]);

    public Task<ImmutableArray<RoomId>> GetFrequentRoomIdsAsync(int limit, CancellationToken ct) =>
        Task.FromResult<ImmutableArray<RoomId>>([
            .. _state
                .VisitsByRoomId.OrderByDescending(x => x.Value.Visits)
                .ThenByDescending(x => x.Value.LastVisitUtc)
                .Take(limit)
                .Select(x => x.Key),
        ]);

    private async Task HydrateAsync(CancellationToken ct)
    {
        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        var favouriteRoomIds = await dbCtx
            .PlayerFavouriteRooms.AsNoTracking()
            .Where(x => x.PlayerEntityId == _state.PlayerId.Value)
            .OrderBy(x => x.Id)
            .Select(x => x.RoomEntityId)
            .ToListAsync(ct);

        var savedSearches = await dbCtx
            .PlayerNavigatorSavedSearches.AsNoTracking()
            .Where(x => x.PlayerEntityId == _state.PlayerId.Value)
            .OrderBy(x => x.Id)
            .Select(x => new
            {
                x.SearchId,
                x.SearchCode,
                x.Filter,
            })
            .ToListAsync(ct);

        var collapsedSearchCodes = await dbCtx
            .PlayerNavigatorCollapsedCategories.AsNoTracking()
            .Where(x => x.PlayerEntityId == _state.PlayerId.Value)
            .Select(x => x.SearchCode)
            .ToListAsync(ct);

        var viewModes = await dbCtx
            .PlayerNavigatorViewModes.AsNoTracking()
            .Where(x => x.PlayerEntityId == _state.PlayerId.Value)
            .Select(x => new { x.SearchCode, x.ViewMode })
            .ToListAsync(ct);

        var visits = await dbCtx
            .RoomEntryLogs.AsNoTracking()
            .Where(x => x.PlayerEntityId == _state.PlayerId.Value)
            .GroupBy(x => x.RoomEntityId)
            .Select(g => new
            {
                RoomId = g.Key,
                Visits = g.Count(),
                LastVisit = g.Max(x => x.CreatedAt),
            })
            .OrderByDescending(x => x.LastVisit)
            .Take(_playerConfig.NavigatorHistoryRooms)
            .ToListAsync(ct);

        _state.FavouriteRoomIds.Clear();
        _state.FavouriteRoomIds.AddRange(favouriteRoomIds.Select(RoomId.Parse));

        _state.SavedSearches.Clear();
        _state.SavedSearches.AddRange(
            savedSearches.Select(x => CreateSavedSearch(x.SearchId, x.SearchCode, x.Filter))
        );
        _state.NextSavedSearchId =
            savedSearches.Count == 0 ? 1 : savedSearches.Max(x => x.SearchId) + 1;

        _state.CollapsedSearchCodes.Clear();
        _state.CollapsedSearchCodes.UnionWith(collapsedSearchCodes);

        _state.ViewModes.Clear();

        foreach (var viewMode in viewModes)
            _state.ViewModes[viewMode.SearchCode] = viewMode.ViewMode;

        _state.VisitsByRoomId.Clear();

        foreach (var visit in visits)
            _state.VisitsByRoomId[RoomId.Parse(visit.RoomId)] = new RoomVisitStats(
                visit.Visits,
                visit.LastVisit
            );

        _state.PendingVisits.Clear();
        _state.PreferencesVersion = 0;
        _state.PersistedPreferencesVersion = 0;
    }

    private async Task FlushAsync(CancellationToken ct)
    {
        if (_state.PreferencesVersion != _state.PersistedPreferencesVersion)
            await FlushPreferencesAsync(ct);

        if (_state.PendingVisits.Count > 0)
            await FlushVisitsAsync(ct);
    }

    /// <summary>
    /// Reconciles the player's preference rows with memory in one save. A failure keeps the
    /// state dirty, so the next tick retries.
    /// </summary>
    private async Task FlushPreferencesAsync(CancellationToken ct)
    {
        var version = _state.PreferencesVersion;
        var favouriteRoomIds = _state.FavouriteRoomIds.Select(x => x.Value).ToHashSet();
        var savedSearches = _state.SavedSearches.ToDictionary(x => x.Id);
        var collapsedSearchCodes = _state.CollapsedSearchCodes.ToHashSet(StringComparer.Ordinal);
        var viewModes = new Dictionary<string, NavigatorViewModeType>(
            _state.ViewModes,
            StringComparer.Ordinal
        );

        try
        {
            await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

            var favouriteRows = await dbCtx
                .PlayerFavouriteRooms.Where(x => x.PlayerEntityId == _state.PlayerId.Value)
                .ToListAsync(ct);

            // Remove() on the snapshot sets leaves only the rows still to be inserted.
            dbCtx.PlayerFavouriteRooms.RemoveRange(
                favouriteRows.Where(x => !favouriteRoomIds.Remove(x.RoomEntityId)).ToList()
            );
            dbCtx.PlayerFavouriteRooms.AddRange(
                favouriteRoomIds.Select(roomId => new PlayerFavoriteRoomsEntity
                {
                    PlayerEntityId = _state.PlayerId.Value,
                    RoomEntityId = roomId,
                    PlayerEntity = null!,
                    RoomEntity = null!,
                })
            );

            var savedSearchRows = await dbCtx
                .PlayerNavigatorSavedSearches.Where(x => x.PlayerEntityId == _state.PlayerId.Value)
                .ToListAsync(ct);

            dbCtx.PlayerNavigatorSavedSearches.RemoveRange(
                savedSearchRows.Where(x => !savedSearches.Remove(x.SearchId)).ToList()
            );
            dbCtx.PlayerNavigatorSavedSearches.AddRange(
                savedSearches.Values.Select(x => new PlayerNavigatorSavedSearchEntity
                {
                    PlayerEntityId = _state.PlayerId.Value,
                    SearchId = x.Id,
                    SearchCode = x.SearchCode,
                    Filter = x.Filter,
                })
            );

            var collapsedRows = await dbCtx
                .PlayerNavigatorCollapsedCategories.Where(x =>
                    x.PlayerEntityId == _state.PlayerId.Value
                )
                .ToListAsync(ct);

            dbCtx.PlayerNavigatorCollapsedCategories.RemoveRange(
                collapsedRows.Where(x => !collapsedSearchCodes.Remove(x.SearchCode)).ToList()
            );
            dbCtx.PlayerNavigatorCollapsedCategories.AddRange(
                collapsedSearchCodes.Select(code => new PlayerNavigatorCollapsedCategoryEntity
                {
                    PlayerEntityId = _state.PlayerId.Value,
                    SearchCode = code,
                })
            );

            var viewModeRows = await dbCtx
                .PlayerNavigatorViewModes.Where(x => x.PlayerEntityId == _state.PlayerId.Value)
                .ToListAsync(ct);

            foreach (var row in viewModeRows)
            {
                if (viewModes.Remove(row.SearchCode, out var viewMode))
                    row.ViewMode = viewMode;
                else
                    dbCtx.PlayerNavigatorViewModes.Remove(row);
            }

            dbCtx.PlayerNavigatorViewModes.AddRange(
                viewModes.Select(x => new PlayerNavigatorViewModeEntity
                {
                    PlayerEntityId = _state.PlayerId.Value,
                    SearchCode = x.Key,
                    ViewMode = x.Value,
                })
            );

            await dbCtx.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to flush navigator preferences for player {PlayerId}",
                _state.PlayerId
            );

            return;
        }

        // Only mark clean if nothing changed while the write was in flight.
        if (_state.PreferencesVersion == version)
            _state.PersistedPreferencesVersion = version;
    }

    /// <summary>
    /// Inserts buffered room visits in one save. A failed batch stays buffered and is retried on
    /// the next tick; only after repeated failures is it given up, so a persistent error (such as
    /// a deleted room) cannot block history forever.
    /// </summary>
    private async Task FlushVisitsAsync(CancellationToken ct)
    {
        var visits = _state.PendingVisits.ToList();

        try
        {
            await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

            dbCtx.RoomEntryLogs.AddRange(
                visits.Select(roomId => new RoomEntryLogEntity
                {
                    RoomEntityId = roomId.Value,
                    PlayerEntityId = _state.PlayerId.Value,
                })
            );

            await dbCtx.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _state.FailedVisitWrites++;

            if (_state.FailedVisitWrites < _playerConfig.NavigatorVisitWriteAttempts)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to write {VisitCount} room visits for player {PlayerId} (attempt {Attempt}); retrying",
                    visits.Count,
                    _state.PlayerId,
                    _state.FailedVisitWrites
                );

                return;
            }

            _logger.LogError(
                ex,
                "Dropped {VisitCount} room visits for player {PlayerId} after {Attempts} failed writes",
                visits.Count,
                _state.PlayerId,
                _state.FailedVisitWrites
            );
        }

        // Visits recorded while the write was in flight stay buffered for the next tick.
        _state.PendingVisits.RemoveRange(0, Math.Min(visits.Count, _state.PendingVisits.Count));
        _state.FailedVisitWrites = 0;
    }

    private void TrimVisitHistory()
    {
        var excess = _state.VisitsByRoomId.Count - _playerConfig.NavigatorHistoryRooms;

        if (excess <= 0)
            return;

        var oldest = _state
            .VisitsByRoomId.OrderBy(x => x.Value.LastVisitUtc)
            .Take(excess)
            .Select(x => x.Key)
            .ToList();

        foreach (var roomId in oldest)
            _state.VisitsByRoomId.Remove(roomId);
    }

    private static NavigatorQuickLinkSnapshot CreateSavedSearch(
        int id,
        string searchCode,
        string filter
    ) =>
        new()
        {
            Id = id,
            SearchCode = searchCode,
            Filter = filter,
            Localization = string.Empty,
        };

    private Task SendSavedSearchesAsync(CancellationToken ct) =>
        _grainFactory.SendComposerToPlayerAsync(
            _state.PlayerId,
            new NavigatorSavedSearchesMessage { SavedSearches = [.. _state.SavedSearches] },
            ct
        );

    private static bool IsValidSearchCode(string searchCode) =>
        !string.IsNullOrWhiteSpace(searchCode)
        && searchCode.Length <= PlayerNavigatorSavedSearchEntity.SEARCH_CODE_MAX_LENGTH;
}
