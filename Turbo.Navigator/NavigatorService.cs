using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Navigator.Configuration;
using Turbo.Primitives.Navigator;
using Turbo.Primitives.Navigator.Enums;
using Turbo.Primitives.Navigator.Snapshots;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Snapshots.Navigator;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Primitives.Texts;

namespace Turbo.Navigator;

/// <summary>
/// Builds navigator results. Room rows come from the provider's short-lived cache; every active
/// room is then replaced by its live copy from the room directory (fetched once per request), so
/// population, score, tags, staff picks and events are current without touching the database.
/// </summary>
public sealed class NavigatorService(
    ILogger<INavigatorService> logger,
    INavigatorProvider navigatorProvider,
    IGrainFactory grainFactory,
    IOptions<NavigatorConfig> config
) : INavigatorService
{
    private readonly ILogger<INavigatorService> _logger = logger;
    private readonly INavigatorProvider _navigatorProvider = navigatorProvider;
    private readonly IGrainFactory _grainFactory = grainFactory;
    private readonly NavigatorConfig _config = config.Value;

    public int FavouriteRoomLimit => _config.MaxFavouriteRooms;
    public int MaxSearchCodeLength => _config.MaxSearchCodeLength;
    public int MaxTagsPerRoom => _config.MaxTagsPerRoom;
    public int MaxTagLength => _config.MaxTagLength;
    public TimeSpan RoomEventDuration => TimeSpan.FromMinutes(_config.RoomEventDurationMinutes);

    public async Task<ImmutableArray<NavigatorTopLevelContextSnapshot>> GetTopLevelContextAsync() =>
        await _navigatorProvider.GetTopLevelContextsAsync().ConfigureAwait(false);

    public async Task<ImmutableArray<NavigatorSearchResultBlockSnapshot>> SearchAsync(
        PlayerId playerId,
        string searchCode,
        string filter,
        CancellationToken ct
    )
    {
        // Both values are echoed back to the client and used as cache and preference keys.
        var code = ClientText.Truncate(searchCode, _config.MaxSearchCodeLength);
        var filterText = ClientText.Truncate(filter, _config.MaxSearchCodeLength);
        var preferences = await _grainFactory
            .GetPlayerNavigatorGrain(playerId)
            .GetSnapshotAsync(ct)
            .ConfigureAwait(false);
        var query = new SearchQuery(
            playerId,
            preferences,
            await GetLiveRoomsAsync(ct).ConfigureAwait(false)
        );

        if (filterText.Length > 0)
        {
            var (filterType, value) = ParseFilter(filterText);
            var rooms = await SearchByFilterAsync(query, filterType, value, ct)
                .ConfigureAwait(false);

            return
            [
                CreateBlock(
                    query,
                    code,
                    string.Empty,
                    rooms,
                    NavigatorActionAllowedType.Collapsed,
                    _config.SearchResultLimit
                ),
            ];
        }

        return code switch
        {
            NavigatorSearchCodes.OFFICIAL_VIEW => await CreatePreviewBlocksAsync(
                    query,
                    [(NavigatorSearchCodes.OFFICIAL_ROOT, string.Empty)],
                    keepEmpty: true,
                    ct
                )
                .ConfigureAwait(false),
            NavigatorSearchCodes.HOTEL_VIEW => await CreatePreviewBlocksAsync(
                    query,
                    [
                        (NavigatorSearchCodes.POPULAR, string.Empty),
                        .. GetFlatCategoriesForPlayer(playerId)
                            .Select(x => (NavigatorSearchCodes.Category(x.Name), x.Name)),
                    ],
                    keepEmpty: false,
                    ct
                )
                .ConfigureAwait(false),
            NavigatorSearchCodes.MYWORLD_VIEW => await CreatePreviewBlocksAsync(
                    query,
                    [.. NavigatorSearchCodes.MyWorldSections.Select(x => (x, string.Empty))],
                    keepEmpty: false,
                    ct
                )
                .ConfigureAwait(false),
            NavigatorSearchCodes.ROOMADS_VIEW => await CreatePreviewBlocksAsync(
                    query,
                    [
                        (NavigatorSearchCodes.TOP_PROMOTIONS, string.Empty),
                        (NavigatorSearchCodes.NEW_ADS, string.Empty),
                        .. GetEventCategories()
                            .Where(x => x.Visible)
                            .Select(x => (NavigatorSearchCodes.EventCategory(x.Name), x.Name)),
                    ],
                    keepEmpty: false,
                    ct
                )
                .ConfigureAwait(false),
            _ => await CreateSectionBlockAsync(query, code, ct).ConfigureAwait(false),
        };
    }

    public async Task<ImmutableArray<NavigatorSearchResultSnapshot>> SearchRoomsAsync(
        PlayerId playerId,
        NavigatorSearchType searchType,
        string searchParam,
        CancellationToken ct
    )
    {
        var limit = _config.SearchResultLimit;
        var param = ClientText.Truncate(searchParam, _config.MaxSearchCodeLength);
        var query = new SearchQuery(
            playerId,
            Preferences: null,
            await GetLiveRoomsAsync(ct).ConfigureAwait(false)
        );

        var rooms = searchType switch
        {
            NavigatorSearchType.PopularRooms or NavigatorSearchType.Categories => int.TryParse(
                param,
                out var categoryId
            )
            && categoryId > 0
                ? await GetCategoryRoomsAsync(query, categoryId, limit, ct).ConfigureAwait(false)
                : await GetPopularRoomsAsync(query, limit, ct).ConfigureAwait(false),
            NavigatorSearchType.RecommendedRooms => await GetPopularRoomsAsync(query, limit, ct)
                .ConfigureAwait(false),
            NavigatorSearchType.RoomsWithHighestScore => await GetHighestScoredRoomsAsync(
                    query,
                    limit,
                    ct
                )
                .ConfigureAwait(false),
            NavigatorSearchType.OfficialRooms => await GetLegacySectionAsync(
                    NavigatorSearchCodes.OFFICIAL_ROOT
                )
                .ConfigureAwait(false),
            NavigatorSearchType.Events or NavigatorSearchType.EventsByCategory =>
                await GetLegacySectionAsync(NavigatorSearchCodes.NEW_ADS).ConfigureAwait(false),
            NavigatorSearchType.TextSearch => await SearchByLegacyFilterAsync(
                    NavigatorSearchFilterType.Anything
                )
                .ConfigureAwait(false),
            NavigatorSearchType.TagSearch => await SearchByLegacyFilterAsync(
                    NavigatorSearchFilterType.Tag
                )
                .ConfigureAwait(false),
            NavigatorSearchType.RoomNameSearch => await SearchByLegacyFilterAsync(
                    NavigatorSearchFilterType.RoomName
                )
                .ConfigureAwait(false),
            NavigatorSearchType.ByOwner => await SearchByLegacyFilterAsync(
                    NavigatorSearchFilterType.Owner
                )
                .ConfigureAwait(false),
            NavigatorSearchType.MyRooms => await GetLegacySectionAsync(
                    NavigatorSearchCodes.MY_ROOMS
                )
                .ConfigureAwait(false),
            NavigatorSearchType.MyFavourites => await GetLegacySectionAsync(
                    NavigatorSearchCodes.FAVOURITES
                )
                .ConfigureAwait(false),
            NavigatorSearchType.MyHistory => await GetLegacySectionAsync(
                    NavigatorSearchCodes.HISTORY
                )
                .ConfigureAwait(false),
            NavigatorSearchType.MyFrequentHistory => await GetLegacySectionAsync(
                    NavigatorSearchCodes.FREQUENT_HISTORY
                )
                .ConfigureAwait(false),
            NavigatorSearchType.MyFriendsRooms => await GetLegacySectionAsync(
                    NavigatorSearchCodes.FRIENDS_ROOMS
                )
                .ConfigureAwait(false),
            NavigatorSearchType.RoomsWhereMyFriendsAre => await GetLegacySectionAsync(
                    NavigatorSearchCodes.WITH_FRIENDS
                )
                .ConfigureAwait(false),
            NavigatorSearchType.MyRoomRights => await GetLegacySectionAsync(
                    NavigatorSearchCodes.WITH_RIGHTS
                )
                .ConfigureAwait(false),
            NavigatorSearchType.MyGuildBases => await GetLegacySectionAsync(
                    NavigatorSearchCodes.GROUPS
                )
                .ConfigureAwait(false),
            NavigatorSearchType.GuildBases => await GetGuildBaseRoomsAsync(query, limit, ct)
                .ConfigureAwait(false),
            NavigatorSearchType.GroupNameSearch => await SearchGuildsByNameAsync(
                    query,
                    param,
                    limit,
                    ct
                )
                .ConfigureAwait(false),
            // No competitions exist yet.
            _ => [],
        };

        return [.. rooms.Select(x => ToSearchResult(x, query))];

        async Task<List<RoomInfoSnapshot>> GetLegacySectionAsync(string code) =>
            await GetSectionRoomsAsync(query, code, limit, ct).ConfigureAwait(false) ?? [];

        Task<List<RoomInfoSnapshot>> SearchByLegacyFilterAsync(NavigatorSearchFilterType filterType)
        {
            // The legacy client sends typed searches with the same prefixes as the new navigator.
            var (parsedType, parsedValue) = ParseFilter(param);

            return SearchByFilterAsync(
                query,
                parsedType == NavigatorSearchFilterType.Anything ? filterType : parsedType,
                parsedValue,
                ct
            );
        }
    }

    public ImmutableArray<NavigatorFlatCategorySnapshot> GetFlatCategoriesForPlayer(
        PlayerId playerId
    ) =>
        // There is no staff rank yet, so every player is treated as a rank-one non-staff user.
        [
            .. _navigatorProvider
                .GetFlatCategories()
                .Where(x => x.Visible && !x.StaffOnly && x.MinRank <= 1),
        ];

    public ImmutableArray<NavigatorEventCategorySnapshot> GetEventCategories() =>
        _navigatorProvider.GetEventCategories();

    public async Task<ImmutableArray<NavigatorPopularTagSnapshot>> GetPopularTagsAsync(
        CancellationToken ct
    )
    {
        var cachedTags = await _navigatorProvider.GetRoomTagsAsync(ct).ConfigureAwait(false);
        var live = await GetLiveRoomsAsync(ct).ConfigureAwait(false);

        // Active rooms carry their current tags; the cached rows cover the rest.
        var tagsByRoom = cachedTags.ToDictionary(x => x.RoomId, x => x.Tags);

        foreach (var room in live.Values)
        {
            if (room.DoorMode == RoomDoorModeType.Invisible || room.Tags.IsDefaultOrEmpty)
                tagsByRoom.Remove(room.RoomId);
            else
                tagsByRoom[room.RoomId] = room.Tags;
        }

        return
        [
            .. tagsByRoom
                .SelectMany(x =>
                    x.Value.Select(tag =>
                        (Tag: tag, Users: live.GetValueOrDefault(x.Key)?.Population ?? 0)
                    )
                )
                .GroupBy(x => x.Tag, StringComparer.OrdinalIgnoreCase)
                .Select(g => new
                {
                    Tag = g.Key,
                    Users = g.Sum(x => x.Users),
                    Rooms = g.Count(),
                })
                .OrderByDescending(x => x.Users)
                .ThenByDescending(x => x.Rooms)
                .ThenBy(x => x.Tag, StringComparer.Ordinal)
                .Take(_config.PopularTagsLimit)
                .Select(x => new NavigatorPopularTagSnapshot { Tag = x.Tag, UserCount = x.Users }),
        ];
    }

    public async Task<ImmutableArray<NavigatorSearchResultSnapshot>> GetOfficialRoomsAsync(
        CancellationToken ct
    )
    {
        var query = new SearchQuery(
            PlayerId: PlayerId.Invalid,
            Preferences: null,
            await GetLiveRoomsAsync(ct).ConfigureAwait(false)
        );
        var rooms = await GetSectionRoomsAsync(
                query,
                NavigatorSearchCodes.OFFICIAL_ROOT,
                _config.SearchResultLimit,
                ct
            )
            .ConfigureAwait(false);

        return [.. (rooms ?? []).Select(x => ToSearchResult(x, query))];
    }

    public async Task<(bool CanCreate, int RoomLimit)> CanCreateRoomAsync(
        PlayerId playerId,
        CancellationToken ct
    )
    {
        await SyncListingsAsync(includeActiveRooms: false, ct).ConfigureAwait(false);

        var roomCount = await _navigatorProvider
            .GetRoomCountForOwnerAsync(playerId, ct)
            .ConfigureAwait(false);

        return (roomCount < _config.MaxRoomsPerPlayer, _config.MaxRoomsPerPlayer);
    }

    public async Task<RoomId?> CreateRoomAsync(
        PlayerId playerId,
        string name,
        string description,
        string modelName,
        int categoryId,
        int playersMax,
        RoomTradeModeType tradeType,
        CancellationToken ct
    )
    {
        name = name?.Trim() ?? string.Empty;
        description = description?.Trim() ?? string.Empty;

        if (
            name.Length < _config.RoomNameMinLength
            || name.Length > _config.RoomNameMaxLength
            || description.Length > _config.RoomDescriptionMaxLength
            || playersMax <= 0
            || playersMax > _config.MaxPlayersLimit
            || !Enum.IsDefined(tradeType)
        )
        {
            _logger.LogWarning(
                "Rejected room creation by player {PlayerId}: name length {NameLength}, description length {DescriptionLength}, players max {PlayersMax}, trade type {TradeType}",
                playerId,
                name.Length,
                description.Length,
                playersMax,
                tradeType
            );

            return null;
        }

        var (canCreate, _) = await CanCreateRoomAsync(playerId, ct).ConfigureAwait(false);

        if (!canCreate)
            return null;

        var modelId = await _navigatorProvider
            .GetRoomModelIdByNameAsync(modelName ?? string.Empty, ct)
            .ConfigureAwait(false);

        if (modelId is null)
        {
            _logger.LogWarning(
                "Rejected room creation by player {PlayerId}: unknown model {ModelName}",
                playerId,
                modelName
            );

            return null;
        }

        int? category = GetFlatCategoriesForPlayer(playerId).Any(x => x.Id == categoryId)
            ? categoryId
            : null;

        var roomId = await _navigatorProvider
            .CreateRoomAsync(
                playerId,
                name,
                description,
                modelId.Value,
                category,
                playersMax,
                tradeType,
                ct
            )
            .ConfigureAwait(false);

        // Published after the insert commits, so no silo can cache a listing without the room.
        List<string> changedKeys =
        [
            NavigatorListingKeys.Owner(playerId),
            NavigatorListingKeys.OwnerRoomCount(playerId),
            NavigatorListingKeys.SEARCH,
        ];

        if (category is not null)
            changedKeys.Add(NavigatorListingKeys.Category(category.Value));

        await _grainFactory
            .GetRoomDirectoryGrain()
            .PublishListingChangesAsync(changedKeys, ct)
            .ConfigureAwait(false);

        return roomId;
    }

    public async Task<RoomId?> GetRandomPromotedRoomAsync(
        string eventCategory,
        CancellationToken ct
    )
    {
        var category = GetEventCategories()
            .FirstOrDefault(x =>
                string.Equals(x.Name, eventCategory, StringComparison.OrdinalIgnoreCase)
            );
        var query = new SearchQuery(
            PlayerId: PlayerId.Invalid,
            Preferences: null,
            await GetLiveRoomsAsync(ct).ConfigureAwait(false)
        );
        var rooms = await GetEventRoomsAsync(query, category?.Id, _config.SearchResultLimit, ct)
            .ConfigureAwait(false);

        return rooms.Count == 0 ? null : rooms[Random.Shared.Next(rooms.Count)].RoomId;
    }

    public bool CanManageStaffPicks(PlayerId playerId) =>
        _config.StaffPickPlayerIds.Contains(playerId.Value);

    public async Task<bool> RoomExistsAsync(RoomId roomId, CancellationToken ct)
    {
        if (roomId.Value <= 0)
            return false;

        await SyncListingsAsync(includeActiveRooms: false, ct).ConfigureAwait(false);

        var rooms = await _navigatorProvider.GetRoomsByIdsAsync([roomId], ct).ConfigureAwait(false);

        return rooms.Count > 0;
    }

    public async Task AddFavouriteRoomAsync(PlayerId playerId, RoomId roomId, CancellationToken ct)
    {
        // Checked here against the room cache, so the grain can persist favourites in batches.
        if (!await RoomExistsAsync(roomId, ct).ConfigureAwait(false))
            return;

        await _grainFactory
            .GetPlayerNavigatorGrain(playerId)
            .AddFavouriteRoomAsync(roomId, _config.MaxFavouriteRooms, ct)
            .ConfigureAwait(false);
    }

    public Task RemoveFavouriteRoomAsync(PlayerId playerId, RoomId roomId, CancellationToken ct) =>
        _grainFactory.GetPlayerNavigatorGrain(playerId).RemoveFavouriteRoomAsync(roomId, ct);

    public async Task<ImmutableArray<RoomId>> GetFavouriteRoomIdsAsync(
        PlayerId playerId,
        CancellationToken ct
    )
    {
        var snapshot = await _grainFactory
            .GetPlayerNavigatorGrain(playerId)
            .GetSnapshotAsync(ct)
            .ConfigureAwait(false);

        return snapshot.FavouriteRoomIds;
    }

    public Task AddSavedSearchAsync(
        PlayerId playerId,
        string searchCode,
        string filter,
        CancellationToken ct
    ) =>
        _grainFactory
            .GetPlayerNavigatorGrain(playerId)
            .AddSavedSearchAsync(searchCode, filter, _config.MaxSavedSearches, ct);

    public Task AddCollapsedSearchCodeAsync(
        PlayerId playerId,
        string searchCode,
        CancellationToken ct
    ) =>
        _grainFactory
            .GetPlayerNavigatorGrain(playerId)
            .AddCollapsedSearchCodeAsync(searchCode, _config.MaxCollapsedSearchCodes, ct);

    public Task SetViewModeAsync(
        PlayerId playerId,
        string searchCode,
        NavigatorViewModeType viewMode,
        CancellationToken ct
    ) =>
        _grainFactory
            .GetPlayerNavigatorGrain(playerId)
            .SetViewModeAsync(searchCode, viewMode, _config.MaxViewModes, ct);

    private async Task<ImmutableArray<NavigatorSearchResultBlockSnapshot>> CreatePreviewBlocksAsync(
        SearchQuery query,
        IReadOnlyList<(string Code, string Text)> sections,
        bool keepEmpty,
        CancellationToken ct
    )
    {
        // One extra room tells us whether the block needs a "show more" action.
        var fetchLimit = _config.BlockPreviewLimit + 1;
        var sectionRooms = await Task.WhenAll(
                sections.Select(section =>
                    GetSectionRoomsAsync(query, section.Code, fetchLimit, ct)
                )
            )
            .ConfigureAwait(false);

        var blocks = new List<NavigatorSearchResultBlockSnapshot>(sections.Count);

        for (var i = 0; i < sections.Count; i++)
        {
            var rooms = sectionRooms[i];

            if (rooms is null || (!keepEmpty && rooms.Count == 0))
                continue;

            var action =
                rooms.Count > _config.BlockPreviewLimit
                    ? NavigatorActionAllowedType.Expanded
                    : NavigatorActionAllowedType.Collapsed;

            blocks.Add(
                CreateBlock(
                    query,
                    sections[i].Code,
                    sections[i].Text,
                    rooms,
                    action,
                    _config.BlockPreviewLimit
                )
            );
        }

        return [.. blocks];
    }

    private async Task<ImmutableArray<NavigatorSearchResultBlockSnapshot>> CreateSectionBlockAsync(
        SearchQuery query,
        string code,
        CancellationToken ct
    )
    {
        var rooms = await GetSectionRoomsAsync(query, code, _config.SearchResultLimit, ct)
            .ConfigureAwait(false);

        if (rooms is null)
            return [];

        var text = NavigatorSearchCodes.GetCategoryName(code);

        return
        [
            CreateBlock(
                query,
                code,
                text,
                rooms,
                NavigatorActionAllowedType.Back,
                _config.SearchResultLimit
            ),
        ];
    }

    private static NavigatorSearchResultBlockSnapshot CreateBlock(
        SearchQuery query,
        string code,
        string text,
        List<RoomInfoSnapshot> rooms,
        NavigatorActionAllowedType action,
        int limit
    ) =>
        new()
        {
            SearchCode = code,
            Text = text,
            ActionAllowed = action,
            Localization = string.Empty,
            ForceClosed = query.Preferences?.CollapsedSearchCodes.Contains(code) ?? false,
            ViewMode = query.Preferences?.GetViewMode(code) ?? NavigatorViewModeType.Rows,
            Results = [.. rooms.Take(limit).Select(x => ToSearchResult(x, query))],
        };

    /// <summary>Rooms for one result section, or null for an unknown code.</summary>
    private async Task<List<RoomInfoSnapshot>?> GetSectionRoomsAsync(
        SearchQuery query,
        string code,
        int limit,
        CancellationToken ct
    )
    {
        var playerId = query.PlayerId;

        switch (code)
        {
            case NavigatorSearchCodes.OFFICIAL_ROOT:
            {
                var cached = await _navigatorProvider
                    .GetStaffPickedRoomsAsync(_config.SearchResultLimit, ct)
                    .ConfigureAwait(false);

                return
                [
                    .. MergeLive(cached, query, x => x.StaffPick && IsPublic(x))
                        .OrderByDescending(x => x.Score)
                        .ThenBy(x => x.RoomId.Value)
                        .Take(limit),
                ];
            }
            case NavigatorSearchCodes.POPULAR:
                return await GetPopularRoomsAsync(query, limit, ct).ConfigureAwait(false);
            case NavigatorSearchCodes.MY_ROOMS:
            {
                var cached = await _navigatorProvider
                    .GetRoomsByOwnersAsync([playerId], _config.SearchResultLimit, ct)
                    .ConfigureAwait(false);

                return
                [
                    .. MergeLive(cached, query, x => x.OwnerId == playerId)
                        .OrderByDescending(x => x.RoomId.Value)
                        .Take(limit),
                ];
            }
            case NavigatorSearchCodes.FAVOURITES:
            {
                var roomIds = await GetFavouriteRoomIdsAsync(playerId, ct).ConfigureAwait(false);

                return await GetRoomsInOrderAsync(query, roomIds, limit, ct).ConfigureAwait(false);
            }
            case NavigatorSearchCodes.HISTORY:
            {
                var roomIds = await _grainFactory
                    .GetPlayerNavigatorGrain(playerId)
                    .GetRecentRoomIdsAsync(Math.Min(limit, _config.HistoryLimit), ct)
                    .ConfigureAwait(false);

                return await GetRoomsInOrderAsync(query, roomIds, limit, ct).ConfigureAwait(false);
            }
            case NavigatorSearchCodes.FREQUENT_HISTORY:
            {
                var roomIds = await _grainFactory
                    .GetPlayerNavigatorGrain(playerId)
                    .GetFrequentRoomIdsAsync(Math.Min(limit, _config.HistoryLimit), ct)
                    .ConfigureAwait(false);

                return await GetRoomsInOrderAsync(query, roomIds, limit, ct).ConfigureAwait(false);
            }
            case NavigatorSearchCodes.FRIENDS_ROOMS:
            {
                var friends = await _grainFactory
                    .GetPlayerMessengerGrain(playerId)
                    .GetFriendsAsync(ct)
                    .ConfigureAwait(false);
                var friendIds = friends.Select(x => x.PlayerId).ToHashSet();
                var cached = await _navigatorProvider
                    .GetRoomsByOwnersAsync(friendIds, _config.SearchResultLimit, ct)
                    .ConfigureAwait(false);

                return
                [
                    .. MergeLive(cached, query, x => friendIds.Contains(x.OwnerId) && IsPublic(x))
                        .OrderByDescending(x => PopulationOf(x, query))
                        .ThenByDescending(x => x.RoomId.Value)
                        .Take(limit),
                ];
            }
            case NavigatorSearchCodes.WITH_FRIENDS:
            {
                var roomIds = await GetRoomsWithOnlineFriendsAsync(playerId, ct)
                    .ConfigureAwait(false);

                return await GetRoomsInOrderAsync(query, roomIds, limit, ct).ConfigureAwait(false);
            }
            case NavigatorSearchCodes.WITH_RIGHTS:
            {
                // Rights are not part of the live copy, so active rooms are only refreshed.
                var cached = await _navigatorProvider
                    .GetRoomsWithRightsAsync(playerId, _config.SearchResultLimit, ct)
                    .ConfigureAwait(false);

                return [.. MergeLive(cached, query, include: null).Take(limit)];
            }
            case NavigatorSearchCodes.GROUPS:
            {
                var roomIds = await GetMyGuildBaseRoomIdsAsync(playerId, ct).ConfigureAwait(false);

                return await GetRoomsInOrderAsync(query, roomIds, limit, ct).ConfigureAwait(false);
            }
            case NavigatorSearchCodes.TOP_PROMOTIONS:
            {
                var rooms = await GetEventRoomsAsync(query, null, _config.SearchResultLimit, ct)
                    .ConfigureAwait(false);

                return
                [
                    .. rooms
                        .OrderByDescending(x => PopulationOf(x, query))
                        .ThenByDescending(x => x.Score)
                        .Take(limit),
                ];
            }
            case NavigatorSearchCodes.NEW_ADS:
                return await GetEventRoomsAsync(query, null, limit, ct).ConfigureAwait(false);
        }

        if (code.StartsWith(NavigatorSearchCodes.CATEGORY_PREFIX, StringComparison.Ordinal))
        {
            var name = NavigatorSearchCodes.GetCategoryName(code);
            var category = GetFlatCategoriesForPlayer(playerId)
                .FirstOrDefault(x =>
                    string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase)
                );

            return category is null
                ? []
                : await GetCategoryRoomsAsync(query, category.Id, limit, ct).ConfigureAwait(false);
        }

        if (code.StartsWith(NavigatorSearchCodes.EVENT_CATEGORY_PREFIX, StringComparison.Ordinal))
        {
            var name = NavigatorSearchCodes.GetCategoryName(code);
            var category = GetEventCategories()
                .FirstOrDefault(x =>
                    string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase)
                );

            return category is null
                ? []
                : await GetEventRoomsAsync(query, category.Id, limit, ct).ConfigureAwait(false);
        }

        return null;
    }

    private async Task<List<RoomInfoSnapshot>> SearchByFilterAsync(
        SearchQuery query,
        NavigatorSearchFilterType filterType,
        string value,
        CancellationToken ct
    )
    {
        // Cached searches are free; each player gets a small quota of searches that reach the
        // database, so typing out many distinct queries cannot flood it.
        if (
            query.PlayerId > 0
            && !_navigatorProvider.IsSearchCached(filterType, value)
            && !await _grainFactory
                .GetPlayerNavigatorGrain(query.PlayerId)
                .TryConsumeSearchQuotaAsync(
                    _config.SearchRateLimitCount,
                    TimeSpan.FromSeconds(_config.SearchRateLimitWindowSeconds),
                    ct
                )
                .ConfigureAwait(false)
        )
            return [];

        var cached = await _navigatorProvider
            .SearchRoomsAsync(filterType, value, _config.SearchResultLimit, ct)
            .ConfigureAwait(false);

        return [.. MergeLive(cached, query, include: null).Where(IsPublic)];
    }

    /// <summary>
    /// Occupied rooms by population, straight from the directory. When fewer than
    /// <paramref name="limit"/> rooms are occupied the list is topped up with the best-rated
    /// rooms, so the view is not empty on a quiet hotel.
    /// </summary>
    private async Task<List<RoomInfoSnapshot>> GetPopularRoomsAsync(
        SearchQuery query,
        int limit,
        CancellationToken ct
    )
    {
        var rooms = query
            .LiveRooms.Values.Where(x => x.Population > 0 && IsVisibleTo(x, query.PlayerId))
            .OrderByDescending(x => x.Population)
            .ThenByDescending(x => x.Score)
            .Take(limit)
            .Cast<RoomInfoSnapshot>()
            .ToList();

        if (rooms.Count >= limit)
            return rooms;

        var topRated = await GetHighestScoredRoomsAsync(query, limit, ct).ConfigureAwait(false);
        var included = rooms.Select(x => x.RoomId).ToHashSet();

        rooms.AddRange(topRated.Where(x => included.Add(x.RoomId)).Take(limit - rooms.Count));

        return rooms;
    }

    private async Task<List<RoomInfoSnapshot>> GetHighestScoredRoomsAsync(
        SearchQuery query,
        int limit,
        CancellationToken ct
    )
    {
        var cached = await _navigatorProvider
            .GetHighestScoredRoomsAsync(_config.SearchResultLimit, ct)
            .ConfigureAwait(false);

        return
        [
            .. MergeLive(cached, query, x => x.Score > 0 && IsPublic(x))
                .OrderByDescending(x => x.Score)
                .ThenByDescending(x => x.RoomId.Value)
                .Take(limit),
        ];
    }

    private async Task<List<RoomInfoSnapshot>> GetCategoryRoomsAsync(
        SearchQuery query,
        int categoryId,
        int limit,
        CancellationToken ct
    )
    {
        var cached = await _navigatorProvider
            .GetRoomsByCategoryAsync(categoryId, _config.SearchResultLimit, ct)
            .ConfigureAwait(false);

        return
        [
            .. MergeLive(cached, query, x => x.CategoryId == categoryId && IsPublic(x))
                .OrderByDescending(x => PopulationOf(x, query))
                .ThenByDescending(x => x.Score)
                .Take(limit),
        ];
    }

    /// <summary>Rooms with a running event, newest event first.</summary>
    private async Task<List<RoomInfoSnapshot>> GetEventRoomsAsync(
        SearchQuery query,
        int? eventCategoryId,
        int limit,
        CancellationToken ct
    )
    {
        var cached = await _navigatorProvider
            .GetRoomsWithActiveEventsAsync(eventCategoryId, _config.SearchResultLimit, ct)
            .ConfigureAwait(false);
        var now = DateTime.UtcNow;

        return
        [
            .. MergeLive(
                    cached,
                    query,
                    x =>
                        IsPublic(x)
                        && x.ActiveEvent is { } evt
                        && evt.IsActiveAt(now)
                        && (eventCategoryId is null || evt.CategoryId == eventCategoryId)
                )
                .OrderByDescending(x => x.ActiveEvent!.CreatedAtUtc)
                .Take(limit),
        ];
    }

    /// <summary>
    /// Resolves rooms by id in the given order: active rooms from the live copy, the rest from
    /// the room cache. Invisible rooms are hidden unless the player owns them.
    /// </summary>
    private async Task<List<RoomInfoSnapshot>> GetRoomsInOrderAsync(
        SearchQuery query,
        IReadOnlyCollection<RoomId> roomIds,
        int limit,
        CancellationToken ct
    )
    {
        if (roomIds.Count == 0)
            return [];

        var inactiveIds = roomIds.Where(x => !query.LiveRooms.ContainsKey(x)).ToList();
        var cached =
            inactiveIds.Count == 0
                ? []
                : await _navigatorProvider
                    .GetRoomsByIdsAsync(inactiveIds, ct)
                    .ConfigureAwait(false);
        var cachedById = cached.ToDictionary(x => x.RoomId);

        return
        [
            .. roomIds
                .Select(id =>
                    query.LiveRooms.TryGetValue(id, out var live)
                        ? live
                        : cachedById.GetValueOrDefault(id)
                )
                .OfType<RoomInfoSnapshot>()
                .Where(x => IsVisibleTo(x, query.PlayerId))
                .Take(limit),
        ];
    }

    private async Task<List<RoomId>> GetRoomsWithOnlineFriendsAsync(
        PlayerId playerId,
        CancellationToken ct
    )
    {
        var friends = await _grainFactory
            .GetPlayerMessengerGrain(playerId)
            .GetFriendsAsync(ct)
            .ConfigureAwait(false);

        var pointers = await Task.WhenAll(
                friends
                    .Where(x => x.Online)
                    .Select(x =>
                        _grainFactory.GetPlayerPresenceGrain(x.PlayerId).GetActiveRoomAsync(ct)
                    )
            )
            .ConfigureAwait(false);

        return [.. pointers.Where(x => x.RoomId.Value > 0).Select(x => x.RoomId).Distinct()];
    }

    /// <summary>
    /// Brings this silo's cache up to date with the room directory's change log and returns the
    /// live copy of every active room. Called before any cached listing is read.
    /// </summary>
    private async Task<IReadOnlyDictionary<RoomId, RoomActiveSnapshot>> GetLiveRoomsAsync(
        CancellationToken ct
    )
    {
        var view = await SyncListingsAsync(includeActiveRooms: true, ct).ConfigureAwait(false);

        return view.ActiveRooms.ToDictionary(x => x.RoomId);
    }

    private async Task<RoomListingViewSnapshot> SyncListingsAsync(
        bool includeActiveRooms,
        CancellationToken ct
    )
    {
        var (epoch, sequence) = _navigatorProvider.ListingPosition;
        var view = await _grainFactory
            .GetRoomDirectoryGrain()
            .GetListingViewAsync(epoch, sequence, includeActiveRooms, ct)
            .ConfigureAwait(false);

        _navigatorProvider.ApplyListingChanges(view);

        return view;
    }

    /// <summary>
    /// Replaces cached rows with the live copy of every active room. With an
    /// <paramref name="include"/> filter, active rooms that now match are added and rows that no
    /// longer match are dropped, so a list reflects changes made since it was cached.
    /// </summary>
    private static IEnumerable<RoomInfoSnapshot> MergeLive(
        IEnumerable<RoomInfoSnapshot> cached,
        SearchQuery query,
        Func<RoomInfoSnapshot, bool>? include
    )
    {
        var merged = cached.Select(x =>
            query.LiveRooms.TryGetValue(x.RoomId, out var live) ? live : x
        );

        if (include is null)
            return merged;

        var result = merged.Where(include).ToList();
        var included = result.Select(x => x.RoomId).ToHashSet();

        result.AddRange(query.LiveRooms.Values.Where(x => include(x) && included.Add(x.RoomId)));

        return result;
    }

    /// <summary>
    /// The homerooms of the groups this player belongs to. Their own grain holds the
    /// memberships and the directory holds each group's room, so neither is queried.
    /// </summary>
    private async Task<List<RoomId>> GetMyGuildBaseRoomIdsAsync(
        PlayerId playerId,
        CancellationToken ct
    )
    {
        var memberships = await _grainFactory
            .GetPlayerGuildGrain(playerId)
            .GetMembershipsAsync(ct)
            .ConfigureAwait(false);

        if (memberships.Length == 0)
            return [];

        var guilds = await _grainFactory
            .GetGuildDirectoryGrain()
            .GetSummariesAsync([.. memberships.Select(x => x.GroupId)], ct)
            .ConfigureAwait(false);

        return [.. guilds.Select(x => x.RoomId)];
    }

    /// <summary>
    /// The hotel's biggest groups, as the group window's "show groups" link asks for them. How
    /// many come back is the guild module's decision, so the directory caps it rather than this.
    /// </summary>
    private async Task<List<RoomInfoSnapshot>> GetGuildBaseRoomsAsync(
        SearchQuery query,
        int limit,
        CancellationToken ct
    )
    {
        var roomIds = await _grainFactory
            .GetGuildDirectoryGrain()
            .GetGuildBaseRoomIdsAsync(ct)
            .ConfigureAwait(false);

        return await GetRoomsInOrderAsync(query, [.. roomIds], limit, ct).ConfigureAwait(false);
    }

    /// <summary>Groups whose name matches, answered as their homerooms.</summary>
    private async Task<List<RoomInfoSnapshot>> SearchGuildsByNameAsync(
        SearchQuery query,
        string name,
        int limit,
        CancellationToken ct
    )
    {
        var guilds = await _grainFactory
            .GetGuildDirectoryGrain()
            .SearchByNameAsync(name, ct)
            .ConfigureAwait(false);

        return await GetRoomsInOrderAsync(query, [.. guilds.Select(x => x.RoomId)], limit, ct)
            .ConfigureAwait(false);
    }

    private static int PopulationOf(RoomInfoSnapshot room, SearchQuery query) =>
        query.LiveRooms.TryGetValue(room.RoomId, out var live) ? live.Population : 0;

    private static bool IsPublic(RoomInfoSnapshot room) =>
        room.DoorMode != RoomDoorModeType.Invisible && !room.HiddenByBc;

    private static bool IsVisibleTo(RoomInfoSnapshot room, PlayerId playerId) =>
        IsPublic(room) || room.OwnerId == playerId;

    /// <summary>
    /// The room as a search returns it. The info is copied whole rather than field by field:
    /// listing it out is how this quietly stopped carrying <c>HiddenByBc</c> and then
    /// <c>Guild</c>. Population is the only thing the search knows better than the room does.
    /// </summary>
    private static NavigatorSearchResultSnapshot ToSearchResult(
        RoomInfoSnapshot room,
        SearchQuery query
    ) => new(room) { Population = PopulationOf(room, query) };

    private static (NavigatorSearchFilterType Type, string Value) ParseFilter(string filter)
    {
        var separator = filter.IndexOf(':');

        if (separator <= 0)
            return (NavigatorSearchFilterType.Anything, filter);

        var type = filter[..separator].FromLegacyString();

        // An unrecognised prefix is part of the text, e.g. a room called "note: party".
        return type == NavigatorSearchFilterType.Anything
            ? (NavigatorSearchFilterType.Anything, filter)
            : (type, filter[(separator + 1)..].Trim());
    }

    /// <param name="PlayerId">The player the results are for.</param>
    /// <param name="Preferences">Block preferences; null for legacy searches.</param>
    /// <param name="LiveRooms">Active rooms from the directory, fetched once per request.</param>
    private sealed record SearchQuery(
        PlayerId PlayerId,
        PlayerNavigatorSnapshot? Preferences,
        IReadOnlyDictionary<RoomId, RoomActiveSnapshot> LiveRooms
    );
}
