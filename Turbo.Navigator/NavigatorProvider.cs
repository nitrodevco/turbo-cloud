using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Turbo.Database.Context;
using Turbo.Database.Entities.Room;
using Turbo.Navigator.Configuration;
using Turbo.Primitives.Navigator;
using Turbo.Primitives.Navigator.Enums;
using Turbo.Primitives.Navigator.Snapshots;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Snapshots;

namespace Turbo.Navigator;

/// <summary>
/// Navigator data access. Room queries are cached per silo and concurrent requests for the same
/// key share one database round trip. Entries are evicted as soon as the room directory reports
/// the listing changed (see <see cref="ApplyListingChanges"/>), so the TTLs only bound memory.
/// Lists always hold <see cref="NavigatorConfig.SearchResultLimit"/> rooms and are trimmed per
/// call, so previews and full lists share one entry.
/// </summary>
public sealed class NavigatorProvider : INavigatorProvider, IDisposable
{
    private readonly IDbContextFactory<TurboDbContext> _dbCtxFactory;
    private readonly ILogger<NavigatorProvider> _logger;
    private readonly NavigatorConfig _config;
    private readonly MemoryCache _cache;
    private readonly TimeSpan _resultTtl;
    private readonly TimeSpan _roomTtl;

    private readonly Lock _listingLock = new();
    private (Guid Epoch, long Sequence) _listingPosition = (Guid.Empty, -1);

    // Text searches have unbounded keys, so they share one token that is cancelled to drop them all.
    private CancellationTokenSource _searchGeneration = new();

    private ImmutableArray<NavigatorTopLevelContextSnapshot> _topLevelContexts = [];
    private ImmutableArray<NavigatorFlatCategorySnapshot> _flatCategories = [];
    private ImmutableArray<NavigatorEventCategorySnapshot> _eventCategories = [];

    public NavigatorProvider(
        IDbContextFactory<TurboDbContext> dbCtxFactory,
        IOptions<NavigatorConfig> config,
        ILogger<NavigatorProvider> logger
    )
    {
        _dbCtxFactory = dbCtxFactory;
        _logger = logger;
        _config = config.Value;
        _cache = new MemoryCache(new MemoryCacheOptions { SizeLimit = _config.CacheSizeLimit });
        _resultTtl = TimeSpan.FromSeconds(_config.ResultCacheSeconds);
        _roomTtl = TimeSpan.FromSeconds(_config.RoomCacheSeconds);
    }

    public void Dispose()
    {
        _cache.Dispose();
        _searchGeneration.Dispose();
    }

    public (Guid Epoch, long Sequence) ListingPosition
    {
        get
        {
            lock (_listingLock)
                return _listingPosition;
        }
    }

    public void ApplyListingChanges(RoomListingViewSnapshot view)
    {
        lock (_listingLock)
        {
            if (view.IsReset)
            {
                _cache.Compact(1.0);
                ResetSearches();
            }
            else if (
                view.Epoch == _listingPosition.Epoch
                && view.Sequence <= _listingPosition.Sequence
            )
            {
                // A concurrent request already applied this position.
                return;
            }
            else
            {
                foreach (var key in view.ChangedKeys)
                    Evict(key);
            }

            _listingPosition = (view.Epoch, view.Sequence);
        }
    }

    public bool IsSearchCached(NavigatorSearchFilterType filterType, string value) =>
        _cache.TryGetValue(
            SearchKey(filterType, value.Trim()),
            out Task<List<RoomInfoSnapshot>>? task
        ) && task is { IsCompletedSuccessfully: true };

    private void Evict(string key)
    {
        switch (key)
        {
            case NavigatorListingKeys.SEARCH:
                ResetSearches();
                break;
            case NavigatorListingKeys.EVENTS:
                _cache.Remove(EventsKey(null));

                foreach (var category in _eventCategories)
                    _cache.Remove(EventsKey(category.Id));
                break;
            default:
                _cache.Remove(key);
                break;
        }
    }

    private void ResetSearches()
    {
        // Not disposed: a request may still be registering an entry against the old token.
        Interlocked.Exchange(ref _searchGeneration, new CancellationTokenSource()).Cancel();
    }

    public Task<ImmutableArray<NavigatorTopLevelContextSnapshot>> GetTopLevelContextsAsync() =>
        Task.FromResult(_topLevelContexts);

    public ImmutableArray<NavigatorFlatCategorySnapshot> GetFlatCategories() => _flatCategories;

    public ImmutableArray<NavigatorEventCategorySnapshot> GetEventCategories() => _eventCategories;

    public async Task<List<RoomInfoSnapshot>> GetRoomsByIdsAsync(
        IReadOnlyCollection<RoomId> roomIds,
        CancellationToken ct
    )
    {
        var found = new Dictionary<RoomId, RoomInfoSnapshot>(roomIds.Count);
        var missing = new List<int>();

        foreach (var roomId in roomIds.Distinct())
        {
            if (
                _cache.TryGetValue(NavigatorListingKeys.Room(roomId), out RoomInfoSnapshot? room)
                && room is not null
            )
                found[roomId] = room;
            else
                missing.Add(roomId.Value);
        }

        if (missing.Count > 0)
        {
            var loaded = await QueryRoomsAsync(
                    q => q.Where(x => missing.Contains(x.Id)),
                    missing.Count,
                    ct
                )
                .ConfigureAwait(false);

            foreach (var room in loaded)
            {
                found[room.RoomId] = room;
                _cache.Set(
                    NavigatorListingKeys.Room(room.RoomId),
                    room,
                    CreateEntryOptions(_roomTtl)
                );
            }
        }

        return [.. roomIds.Select(id => found.GetValueOrDefault(id)).OfType<RoomInfoSnapshot>()];
    }

    public async Task<List<RoomInfoSnapshot>> GetRoomsByOwnersAsync(
        IReadOnlyCollection<PlayerId> ownerIds,
        int limit,
        CancellationToken ct
    )
    {
        if (ownerIds.Count == 0 || limit <= 0)
            return [];

        // Cached per owner, so overlapping friend lists share their entries.
        var roomsByOwner = new Dictionary<PlayerId, List<RoomInfoSnapshot>>(ownerIds.Count);
        var missing = new List<int>();

        foreach (var ownerId in ownerIds.Distinct())
        {
            if (
                _cache.TryGetValue(
                    NavigatorListingKeys.Owner(ownerId),
                    out List<RoomInfoSnapshot>? rooms
                ) && rooms is not null
            )
                roomsByOwner[ownerId] = rooms;
            else
                missing.Add(ownerId.Value);
        }

        if (missing.Count > 0)
        {
            var loaded = await QueryRoomsAsync(
                    q =>
                        q.Where(x => missing.Contains(x.PlayerEntityId))
                            .OrderByDescending(x => x.Id),
                    missing.Count * _config.SearchResultLimit,
                    ct
                )
                .ConfigureAwait(false);
            var loadedByOwner = loaded.ToLookup(x => x.OwnerId);

            foreach (var ownerId in missing.Select(PlayerId.Parse))
            {
                var rooms = loadedByOwner[ownerId].Take(_config.SearchResultLimit).ToList();

                roomsByOwner[ownerId] = rooms;
                _cache.Set(
                    NavigatorListingKeys.Owner(ownerId),
                    rooms,
                    CreateEntryOptions(_resultTtl)
                );
            }
        }

        return
        [
            .. roomsByOwner
                .Values.SelectMany(x => x)
                .OrderByDescending(x => x.RoomId.Value)
                .Take(limit),
        ];
    }

    public Task<List<RoomInfoSnapshot>> GetRoomsWithRightsAsync(
        PlayerId playerId,
        int limit,
        CancellationToken ct
    ) =>
        GetCachedListAsync(
            NavigatorListingKeys.Rights(playerId),
            limit,
            q =>
                q.Where(x => x.RoomRights!.Any(r => r.PlayerEntityId == playerId.Value))
                    .OrderByDescending(x => x.Id),
            ct
        );

    public Task<List<RoomInfoSnapshot>> GetRoomsByCategoryAsync(
        int categoryId,
        int limit,
        CancellationToken ct
    ) =>
        GetCachedListAsync(
            NavigatorListingKeys.Category(categoryId),
            limit,
            q =>
                q.Where(x =>
                        x.NavigatorCategoryEntityId == categoryId
                        && x.DoorMode != RoomDoorModeType.Invisible
                    )
                    .OrderByDescending(x => x.Score)
                    .ThenByDescending(x => x.Id),
            ct
        );

    public Task<List<RoomInfoSnapshot>> GetHighestScoredRoomsAsync(
        int limit,
        CancellationToken ct
    ) =>
        GetCachedListAsync(
            NavigatorListingKeys.HIGHEST_SCORED,
            limit,
            q =>
                q.Where(x => x.Score > 0 && x.DoorMode != RoomDoorModeType.Invisible)
                    .OrderByDescending(x => x.Score)
                    .ThenByDescending(x => x.Id),
            ct
        );

    public Task<List<RoomInfoSnapshot>> GetStaffPickedRoomsAsync(int limit, CancellationToken ct) =>
        GetCachedListAsync(
            NavigatorListingKeys.STAFF_PICKS,
            limit,
            q =>
                q.Where(x => x.StaffPick && x.DoorMode != RoomDoorModeType.Invisible)
                    .OrderByDescending(x => x.Score)
                    .ThenBy(x => x.Id),
            ct
        );

    public Task<List<RoomInfoSnapshot>> GetRoomsWithActiveEventsAsync(
        int? eventCategoryId,
        int limit,
        CancellationToken ct
    ) =>
        GetCachedListAsync(
            EventsKey(eventCategoryId),
            limit,
            q =>
            {
                // Taken when the entry is loaded, so a reload never uses a stale cut-off.
                var now = DateTime.UtcNow;

                return q.Where(x =>
                        x.DoorMode != RoomDoorModeType.Invisible
                        && x.RoomEvents!.Any(e =>
                            e.ExpiresAt > now
                            && (
                                eventCategoryId == null
                                || e.NavigatorEventCategoryEntityId == eventCategoryId
                            )
                        )
                    )
                    .OrderByDescending(x =>
                        x.RoomEvents!.Where(e => e.ExpiresAt > now).Max(e => e.CreatedAt)
                    );
            },
            ct
        );

    public Task<List<RoomInfoSnapshot>> SearchRoomsAsync(
        NavigatorSearchFilterType filterType,
        string value,
        int limit,
        CancellationToken ct
    )
    {
        value = value.Trim();

        if (value.Length == 0 || value.Length > _config.MaxSearchCodeLength)
            return Task.FromResult(new List<RoomInfoSnapshot>());

        var tagPattern = "," + value + ",";

        return GetCachedListAsync(
            SearchKey(filterType, value),
            limit,
            q =>
            {
                q = q.Where(x => x.DoorMode != RoomDoorModeType.Invisible);

                q = filterType switch
                {
                    NavigatorSearchFilterType.RoomName => q.Where(x => x.Name.Contains(value)),
                    NavigatorSearchFilterType.Owner => q.Where(x => x.PlayerEntity.Name == value),
                    NavigatorSearchFilterType.Tag => q.Where(x =>
                        x.Tags != null && ("," + x.Tags + ",").Contains(tagPattern)
                    ),
                    // There are no groups yet, so a group search matches nothing.
                    NavigatorSearchFilterType.Group => q.Where(x => false),
                    _ => q.Where(x =>
                        x.Name.Contains(value)
                        || x.PlayerEntity.Name == value
                        || (x.Tags != null && ("," + x.Tags + ",").Contains(tagPattern))
                    ),
                };

                return q.OrderByDescending(x => x.Score).ThenByDescending(x => x.Id);
            },
            ct
        );
    }

    public Task<List<(RoomId RoomId, ImmutableArray<string> Tags)>> GetRoomTagsAsync(
        CancellationToken ct
    ) => GetOrLoadAsync(NavigatorListingKeys.TAGS, _resultTtl, LoadRoomTagsAsync, ct);

    public Task<int> GetRoomCountForOwnerAsync(PlayerId ownerId, CancellationToken ct) =>
        GetOrLoadAsync(
            NavigatorListingKeys.OwnerRoomCount(ownerId),
            _resultTtl,
            async loadCt =>
            {
                var dbCtx = await _dbCtxFactory.CreateDbContextAsync(loadCt).ConfigureAwait(false);
                await using var dbCtxScope = dbCtx.ConfigureAwait(false);

                return await dbCtx
                    .Rooms.AsNoTracking()
                    .CountAsync(x => x.PlayerEntityId == ownerId.Value, loadCt)
                    .ConfigureAwait(false);
            },
            ct
        );

    public async Task<int?> GetRoomModelIdByNameAsync(string modelName, CancellationToken ct)
    {
        var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        return await dbCtx
            .RoomModels.AsNoTracking()
            .Where(x => x.Name == modelName && x.Enabled && !x.Custom)
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);
    }

    public async Task<RoomId> CreateRoomAsync(
        PlayerId ownerId,
        string name,
        string description,
        int modelId,
        int? categoryId,
        int playersMax,
        RoomTradeModeType tradeType,
        CancellationToken ct
    )
    {
        var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var entity = new RoomEntity
        {
            Name = name,
            Description = description,
            PlayerEntityId = ownerId.Value,
            DoorMode = RoomDoorModeType.Open,
            RoomModelEntityId = modelId,
            NavigatorCategoryEntityId = categoryId,
            UsersNow = 0,
            PlayersMax = playersMax,
            WallHeight = -1,
            HideWalls = false,
            ThicknessWall = RoomThicknessType.Normal,
            ThicknessFloor = RoomThicknessType.Normal,
            AllowBlocking = false,
            AllowPets = false,
            AllowPetsEat = false,
            TradeType = tradeType,
            MuteType = ModSettingType.Owner,
            KickType = ModSettingType.Owner,
            BanType = ModSettingType.Owner,
            ChatFloodType = ChatFloodSensitivityType.Minimal,
            PlayerEntity = null!,
            RoomModelEntity = null!,
        };

        dbCtx.Rooms.Add(entity);

        await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);

        _logger.LogInformation(
            "Player {PlayerId} created room {RoomId} ({RoomName})",
            ownerId,
            entity.Id,
            name
        );

        return RoomId.Parse(entity.Id);
    }

    public async Task ReloadAsync(CancellationToken ct = default)
    {
        var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var topLevelEntities = await dbCtx
            .NavigatorTopLevelContexts.AsNoTracking()
            .Where(x => x.Visible)
            .OrderBy(x => x.OrderNum)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var flatCategoryEntities = await dbCtx
            .NavigatorFlatCategories.AsNoTracking()
            .OrderBy(x => x.OrderNum)
            .ThenBy(x => x.Id)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var eventCategoryEntities = await dbCtx
            .NavigatorEventCategories.AsNoTracking()
            .OrderBy(x => x.Id)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        _topLevelContexts =
        [
            .. topLevelEntities.Select(x => new NavigatorTopLevelContextSnapshot
            {
                SearchCode = x.SearchCode,
                QuickLinks = [],
            }),
        ];

        _flatCategories =
        [
            .. flatCategoryEntities.Select(x => new NavigatorFlatCategorySnapshot
            {
                Id = x.Id,
                Name = x.Name,
                Visible = x.Visible,
                Automatic = x.Automatic,
                AutomaticCategoryKey = x.AutomaticCategory ?? string.Empty,
                GlobalCategoryKey = x.GlobalCategory ?? string.Empty,
                StaffOnly = x.StaffOnly,
                MinRank = x.MinRank,
                OrderNum = x.OrderNum,
            }),
        ];

        _eventCategories =
        [
            .. eventCategoryEntities.Select(x => new NavigatorEventCategorySnapshot
            {
                Id = x.Id,
                Name = x.Name,
                Visible = x.Visible,
            }),
        ];

        _logger.LogInformation(
            "Loaded navigator snapshot: TotalTopLevelContexts={TotalTopLevelContextsCount}, FlatCategories={FlatCategoryCount}, EventCategories={EventCategoryCount}",
            _topLevelContexts.Length,
            _flatCategories.Length,
            _eventCategories.Length
        );
    }

    private async Task<List<RoomInfoSnapshot>> GetCachedListAsync(
        string key,
        int limit,
        Func<IQueryable<RoomEntity>, IQueryable<RoomEntity>> shape,
        CancellationToken ct
    )
    {
        if (limit <= 0)
            return [];

        var rooms = await GetOrLoadAsync(
                key,
                _resultTtl,
                loadCt => QueryRoomsAsync(shape, _config.SearchResultLimit, loadCt),
                ct,
                expiresWithSearches: key.StartsWith(SEARCH_KEY_PREFIX, StringComparison.Ordinal)
            )
            .ConfigureAwait(false);

        // Callers get their own list; the cached one is shared.
        return [.. rooms.Take(limit)];
    }

    /// <summary>
    /// Returns the cached value for <paramref name="key"/>. The in-flight load itself is cached,
    /// so concurrent callers share one database round trip; a failed load is evicted so the next
    /// call retries. The load ignores the caller's token, so one cancelled request cannot fail it
    /// for the others.
    /// </summary>
    private async Task<T> GetOrLoadAsync<T>(
        string key,
        TimeSpan ttl,
        Func<CancellationToken, Task<T>> load,
        CancellationToken ct,
        bool expiresWithSearches = false
    )
    {
        var loadTask = _cache.GetOrCreate(
            key,
            entry =>
            {
                entry.Size = 1;
                entry.AbsoluteExpirationRelativeToNow = ttl;

                // Taken before the load starts, so a change published while it runs still
                // expires the entry.
                if (expiresWithSearches)
                    entry.AddExpirationToken(new CancellationChangeToken(_searchGeneration.Token));

                return load(CancellationToken.None);
            }
        )!;

        try
        {
            return await loadTask.WaitAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (loadTask.IsFaulted || loadTask.IsCanceled)
        {
            _cache.Remove(key);
            _logger.LogError(ex, "Failed to load navigator cache entry {CacheKey}", key);

            throw;
        }
    }

    private async Task<List<(RoomId RoomId, ImmutableArray<string> Tags)>> LoadRoomTagsAsync(
        CancellationToken ct
    )
    {
        var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var rows = await dbCtx
            .Rooms.AsNoTracking()
            .Where(x => x.Tags != null && x.DoorMode != RoomDoorModeType.Invisible)
            .Select(x => new { x.Id, x.Tags })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return [.. rows.Select(x => (RoomId.Parse(x.Id), RoomTags.Parse(x.Tags)))];
    }

    private static MemoryCacheEntryOptions CreateEntryOptions(TimeSpan ttl) =>
        new() { Size = 1, AbsoluteExpirationRelativeToNow = ttl };

    private const string SEARCH_KEY_PREFIX = NavigatorListingKeys.SEARCH + ":";

    private static string SearchKey(NavigatorSearchFilterType filterType, string value) =>
        $"{SEARCH_KEY_PREFIX}{(int)filterType}:{value.ToLowerInvariant()}";

    private static string EventsKey(int? eventCategoryId) =>
        $"{NavigatorListingKeys.EVENTS}:{eventCategoryId?.ToString(CultureInfo.InvariantCulture) ?? "all"}";

    private async Task<List<RoomInfoSnapshot>> QueryRoomsAsync(
        Func<IQueryable<RoomEntity>, IQueryable<RoomEntity>> shape,
        int limit,
        CancellationToken ct
    )
    {
        if (limit <= 0)
            return [];

        var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var rows = await shape(dbCtx.Rooms.AsNoTracking())
            .Take(limit)
            .Select(x => new { Room = x, OwnerName = x.PlayerEntity.Name })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        if (rows.Count == 0)
            return [];

        var roomIds = rows.Select(x => x.Room.Id).ToList();
        var now = DateTime.UtcNow;

        var events = await dbCtx
            .RoomEvents.AsNoTracking()
            .Where(x => roomIds.Contains(x.RoomEntityId) && x.ExpiresAt > now)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var eventsByRoomId = events
            .GroupBy(x => x.RoomEntityId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.Id).First());

        return
        [
            .. rows.Select(row =>
            {
                var room = row.Room;
                var evt = eventsByRoomId.GetValueOrDefault(room.Id);

                return new RoomInfoSnapshot
                {
                    RoomId = room.Id,
                    Name = room.Name,
                    Description = room.Description ?? string.Empty,
                    OwnerId = PlayerId.Parse(room.PlayerEntityId),
                    OwnerName = row.OwnerName,
                    Population = 0,
                    DoorMode = room.DoorMode,
                    PlayersMax = room.PlayersMax,
                    TradeType = room.TradeType,
                    Score = room.Score,
                    Ranking = 0,
                    CategoryId = room.NavigatorCategoryEntityId ?? -1,
                    Tags = RoomTags.Parse(room.Tags),
                    AllowBlocking = room.AllowBlocking,
                    AllowPets = room.AllowPets,
                    AllowPetsEat = room.AllowPetsEat,
                    StaffPick = room.StaffPick,
                    ActiveEvent = evt is null
                        ? null
                        : new RoomEventSnapshot
                        {
                            EventId = evt.Id,
                            RoomId = room.Id,
                            OwnerId = PlayerId.Parse(evt.PlayerEntityId),
                            OwnerName = row.OwnerName,
                            CategoryId = evt.NavigatorEventCategoryEntityId,
                            Name = evt.Name,
                            Description = evt.Description,
                            CreatedAtUtc = evt.CreatedAt,
                            ExpiresAtUtc = evt.ExpiresAt,
                        },
                    LastUpdatedUtc = now,
                };
            }),
        ];
    }
}
