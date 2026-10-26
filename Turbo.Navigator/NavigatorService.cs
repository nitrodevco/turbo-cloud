using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Navigator.Configuration;
using Turbo.Players.Configuration;
using Turbo.Primitives.Moderation;
using Turbo.Primitives.Navigator;
using Turbo.Primitives.Navigator.Enums;
using Turbo.Primitives.Navigator.Snapshots;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Messenger;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Players.Providers;
using Turbo.Primitives.Players.Snapshots.Navigator;
using Turbo.Primitives.Players.Snapshots.Permissions;
using Turbo.Primitives.Quests;
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
    IOptions<NavigatorConfig> config,
    IOptions<PlayerNavigatorConfig> playerNavigatorConfig,
    IPermissionRegistryProvider permissionRegistryProvider,
    IWordFilter wordFilter
) : INavigatorService
{
    private readonly ILogger<INavigatorService> _logger = logger;
    private readonly INavigatorProvider _navigatorProvider = navigatorProvider;
    private readonly IGrainFactory _grainFactory = grainFactory;
    private readonly NavigatorConfig _config = config.Value;
    private readonly PlayerNavigatorConfig _playerNavigatorConfig = playerNavigatorConfig.Value;
    private readonly IPermissionRegistryProvider _permissionRegistryProvider =
        permissionRegistryProvider;
    private readonly IWordFilter _wordFilter = wordFilter;

    public int FavouriteRoomLimit => _playerNavigatorConfig.MaxFavouriteRooms;
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

        // Two different grains, so asked side by side.
        var preferencesTask = _grainFactory.GetPlayerNavigatorGrain(playerId).GetSnapshotAsync(ct);
        var liveRoomsTask = GetLiveRoomsAsync(ct);

        await Task.WhenAll(preferencesTask, liveRoomsTask).ConfigureAwait(false);

        var query = new SearchQuery(
            playerId,
            await preferencesTask.ConfigureAwait(false),
            await liveRoomsTask.ConfigureAwait(false)
        );

        if (filterText.Length > 0)
        {
            var (filterType, value) = ParseFilter(filterText);

            if (code == NavigatorSearchCodes.HOTEL_VIEW)
                return await CreateFilteredHotelViewBlocksAsync(query, filterType, value, ct)
                    .ConfigureAwait(false);

            var filteredSection = await CreateFilteredSectionBlockAsync(
                    query,
                    code,
                    filterType,
                    value,
                    ct
                )
                .ConfigureAwait(false);

            if (filteredSection is not null)
                return [filteredSection];

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
            NavigatorSearchCodes.OFFICIAL_VIEW => await CreatePublicBlocksAsync(query, ct)
                .ConfigureAwait(false),
            NavigatorSearchCodes.HOTEL_VIEW => await CreateHotelViewBlocksAsync(query, ct)
                .ConfigureAwait(false),
            NavigatorSearchCodes.MYWORLD_VIEW => await CreatePreviewBlocksAsync(
                    query,
                    [.. NavigatorSearchCodes.MyWorldSections.Select(x => (x, string.Empty))],
                    keepEmpty: false,
                    ct,
                    _config.MyWorldKeepEmpty ?? NavigatorConfig.HabboMyWorldKeepEmpty
                )
                .ConfigureAwait(false),
            NavigatorSearchCodes.ROOMADS_VIEW => await CreatePreviewBlocksAsync(
                    query,
                    // Habbo's events tab (official client capture 2026-10-13, two samples): top
                    // promotions, then one block per event category; no new_ads block.
                    [
                        (NavigatorSearchCodes.TOP_PROMOTIONS, string.Empty),
                        .. GetEventCategories()
                            .Where(x => x.Visible)
                            // Habbo names these blocks by id (eventcategory__2) and sends no
                            // text: the client titles them from
                            // navigator.searchcode.title.eventcategory__2.
                            .Select(x =>
                                (
                                    NavigatorSearchCodes.EventCategory(
                                        x.Id.ToString(CultureInfo.InvariantCulture)
                                    ),
                                    string.Empty
                                )
                            ),
                    ],
                    keepEmpty: false,
                    ct
                )
                .ConfigureAwait(false),
            _ => await CreatePublicSectionBlockAsync(query, code, ct).ConfigureAwait(false)
                is { } publicBlock
                ? [publicBlock]
                : await CreateSectionBlockAsync(query, code, ct).ConfigureAwait(false),
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
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var categoryId
            )
            && categoryId > 0
                ? await GetCategoryRoomsAsync(query, categoryId, limit, ct).ConfigureAwait(false)
                : await GetPopularRoomsAsync(query, limit, ct).ConfigureAwait(false),
            NavigatorSearchType.RecommendedRooms => GetRecommendedRooms(query, limit),
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
                    NavigatorSearchCodes.MY_GROUPS
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

    public Task<ImmutableArray<NavigatorFlatCategorySnapshot>> GetFlatCategoriesForPlayerAsync(
        PlayerId playerId,
        CancellationToken ct
    ) => GetFlatCategoriesForPlayerAsync(playerId, NavigatorCategoryAccess.CanSee, ct);

    public Task<ImmutableArray<NavigatorFlatCategorySnapshot>> GetKnownFlatCategoriesForPlayerAsync(
        PlayerId playerId,
        CancellationToken ct
    ) => GetFlatCategoriesForPlayerAsync(playerId, NavigatorCategoryAccess.CanKnow, ct);

    private async Task<
        ImmutableArray<NavigatorFlatCategorySnapshot>
    > GetFlatCategoriesForPlayerAsync(
        PlayerId playerId,
        Func<
            NavigatorFlatCategorySnapshot,
            ResolvedPermissionsSnapshot,
            SecurityLevelType,
            bool
        > allowed,
        CancellationToken ct
    )
    {
        var permissions = await _grainFactory
            .GetPlayerPermissionGrain(playerId)
            .GetResolvedAsync(ct)
            .ConfigureAwait(false);

        var level = PermissionProjection.SecurityLevelOf(
            _permissionRegistryProvider.Current,
            permissions
        );

        return
        [
            .. _navigatorProvider.GetFlatCategories().Where(x => allowed(x, permissions, level)),
        ];
    }

    public ImmutableArray<NavigatorEventCategorySnapshot> GetEventCategories() =>
        _navigatorProvider.GetEventCategories();

    /// <remarks>
    /// Habbo's room ad page sends -1 unless the buyer picks a category: its drop menu shows the
    /// first visible category selected, but the purchase data it writes the pick into is made
    /// only after that selection (RoomAdsCatalogWidget.onPurchaseInfoEvent, RoomAdPurchaseData
    /// categoryId -1). The ad goes where the buyer saw it would.
    /// </remarks>
    public int? GetRoomAdCategoryId(int requestedCategoryId)
    {
        var visible = GetEventCategories().Where(x => x.Visible).ToList();

        if (visible.Any(x => x.Id == requestedCategoryId))
            return requestedCategoryId;

        return visible.Count > 0 ? visible[0].Id : null;
    }

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

        var limit = await _grainFactory
            .GetLimitAsync(playerId, PermissionMetaKeys.Limit.ROOMS, _config.MaxRoomsPerPlayer, ct)
            .ConfigureAwait(false);

        return (roomCount < limit, limit);
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

        name = _wordFilter.FilterAndTruncate(name, _config.RoomNameMaxLength);
        description = _wordFilter.FilterAndTruncate(description, _config.RoomDescriptionMaxLength);

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

        int? category = (
            await GetFlatCategoriesForPlayerAsync(playerId, ct).ConfigureAwait(false)
        ).Any(x => x.Id == categoryId)
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

        // A reward track task of that type counts the room (the player's grain ignores it when
        // no task does); the creation does not wait on it.
        _grainFactory
            .GetPlayerRewardTrackGrain(playerId)
            .RecordActionAsync(RewardTrackActionTypes.CREATE_ROOM, "", CancellationToken.None)
            .LogAndForget(_logger, "count the room created by player {PlayerId}", playerId);

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

    public async Task<RoomId?> GetRoomNetworkRoomAsync(
        int networkId,
        RoomId homeRoomId,
        CancellationToken ct
    )
    {
        if (!_config.RoomNetworks.TryGetValue(networkId, out var configured))
            return null;

        await SyncListingsAsync(includeActiveRooms: false, ct).ConfigureAwait(false);

        var rooms = (
            await _navigatorProvider
                .GetRoomsByIdsAsync([.. configured.Distinct().Select(RoomId.Parse)], ct)
                .ConfigureAwait(false)
        )
            .Select(x => x.RoomId)
            .ToList();

        if (rooms.Contains(homeRoomId))
            return homeRoomId;

        return rooms.Count == 0 ? null : rooms[Random.Shared.Next(rooms.Count)];
    }

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
            .AddFavouriteRoomAsync(roomId, ct)
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
        _grainFactory.GetPlayerNavigatorGrain(playerId).AddSavedSearchAsync(searchCode, filter, ct);

    /// <summary>
    /// A Public tab block (<c>official_&lt;id&gt;</c>) stays collapsed only in the client: Habbo's
    /// next collapsed list left it out after two collapses (official client capture 2026-10-26).
    /// </summary>
    public Task AddCollapsedSearchCodeAsync(
        PlayerId playerId,
        string searchCode,
        CancellationToken ct
    ) =>
        NavigatorSearchCodes.GetOfficialCategoryId(searchCode) is not null
            ? Task.CompletedTask
            : _grainFactory
                .GetPlayerNavigatorGrain(playerId)
                .AddCollapsedSearchCodeAsync(searchCode, ct);

    public Task SetViewModeAsync(
        PlayerId playerId,
        string searchCode,
        NavigatorViewModeType viewMode,
        CancellationToken ct
    ) => _grainFactory.GetPlayerNavigatorGrain(playerId).SetViewModeAsync(searchCode, viewMode, ct);

    private async Task<ImmutableArray<NavigatorSearchResultBlockSnapshot>> CreatePreviewBlocksAsync(
        SearchQuery query,
        IReadOnlyList<(string Code, string Text)> sections,
        bool keepEmpty,
        CancellationToken ct,
        IReadOnlyCollection<string>? keepEmptyCodes = null
    )
    {
        // One extra room tells us whether the block needs a "show more" action.
        var sectionRooms = await Task.WhenAll(
                sections.Select(section =>
                    GetSectionRoomsAsync(query, section.Code, PreviewLimit(section.Code) + 1, ct)
                )
            )
            .ConfigureAwait(false);

        var blocks = new List<NavigatorSearchResultBlockSnapshot>(sections.Count);

        for (var i = 0; i < sections.Count; i++)
        {
            var rooms = sectionRooms[i];

            if (
                rooms is null
                || (
                    !keepEmpty
                    && rooms.Count == 0
                    && keepEmptyCodes?.Contains(sections[i].Code) != true
                )
            )
                continue;

            blocks.Add(CreatePreviewBlock(query, sections[i].Code, sections[i].Text, rooms));
        }

        return [.. blocks];
    }

    /// <summary>
    /// Habbo's hotel view (official client capture 2026-10-13 and 2026-10-16, six samples): hot,
    /// popular, recommended, then one block per room category, each left out when it has no
    /// rooms. Every room in them is occupied and open (no doorbell or password room in any of
    /// the six, while a third of the occupied rooms its text searches return have one). Its
    /// <c>nft_all_rooms</c> block is not sent: this hotel has no NFT rooms.
    /// </summary>
    private async Task<
        ImmutableArray<NavigatorSearchResultBlockSnapshot>
    > CreateHotelViewBlocksAsync(SearchQuery query, CancellationToken ct)
    {
        var categories = await GetFlatCategoriesForPlayerAsync(query.PlayerId, ct)
            .ConfigureAwait(false);
        var popular = await GetPopularRoomsAsync(
                query,
                PreviewLimit(NavigatorSearchCodes.POPULAR) + 1,
                ct
            )
            .ConfigureAwait(false);

        IEnumerable<(string Code, string Text, List<RoomInfoSnapshot> Rooms)> sections =
        [
            (
                NavigatorSearchCodes.HOT,
                string.Empty,
                GetHotRooms(query, PreviewLimit(NavigatorSearchCodes.HOT) + 1)
            ),
            (NavigatorSearchCodes.POPULAR, string.Empty, popular),
            (
                NavigatorSearchCodes.RECOMMENDED,
                string.Empty,
                GetRecommendedRooms(query, PreviewLimit(NavigatorSearchCodes.RECOMMENDED) + 1)
            ),
            .. categories.Select(category =>
            {
                var code = NavigatorSearchCodes.Category(category.Name);

                return (
                    code,
                    CategoryText(category),
                    ListedRooms(query)
                        .Where(x => x.CategoryId == category.Id)
                        .Take(PreviewLimit(code) + 1)
                        .ToList<RoomInfoSnapshot>()
                );
            }),
        ];

        return
        [
            .. sections
                .Where(x => x.Rooms.Count > 0)
                .Select(x => CreatePreviewBlock(query, x.Code, x.Text, x.Rooms)),
        ];
    }

    /// <summary>
    /// Habbo's hotel view with text typed into its search (official client capture 2026-10-12,
    /// eight filters, and 2026-10-13): a <c>query</c> block of every matching room, doorbell and
    /// password rooms included, fullest first; then the recommended and category blocks holding
    /// only their own rooms that match. Hot and popular are not sent. A prefixed filter
    /// (<c>owner:</c>, 2026-10-13) is answered with the query block alone.
    /// </summary>
    private async Task<
        ImmutableArray<NavigatorSearchResultBlockSnapshot>
    > CreateFilteredHotelViewBlocksAsync(
        SearchQuery query,
        NavigatorSearchFilterType filterType,
        string value,
        CancellationToken ct
    )
    {
        var matches = await SearchByFilterAsync(query, filterType, value, ct).ConfigureAwait(false);
        // Nothing matching answers no block at all (official client capture 2026-10-16).
        if (matches.Count == 0)
            return [];

        var blocks = new List<NavigatorSearchResultBlockSnapshot>
        {
            CreatePreviewBlock(
                query,
                NavigatorSearchCodes.QUERY,
                string.Empty,
                [.. matches.OrderByDescending(x => PopulationOf(x, query))]
            ),
        };

        if (filterType != NavigatorSearchFilterType.Anything)
            return [.. blocks];

        var text = value.Trim();
        var categories = await GetFlatCategoriesForPlayerAsync(query.PlayerId, ct)
            .ConfigureAwait(false);
        // Each category block is that category's own "show more" list filtered by the text:
        // Habbo's listed the best-rated empty rooms the full list ends with, and none of the
        // matching rooms outside it.
        var categoryRooms = await Task.WhenAll(
                categories.Select(category =>
                    GetCategoryRoomsAsync(query, category.Id, _config.CategorySearchResultLimit, ct)
                )
            )
            .ConfigureAwait(false);

        IEnumerable<(string Code, string Text, IEnumerable<RoomInfoSnapshot> Rooms)> sections =
        [
            (
                NavigatorSearchCodes.RECOMMENDED,
                string.Empty,
                GetRecommendedRooms(query, int.MaxValue)
            ),
            .. categories.Select(
                (category, i) =>
                    (
                        NavigatorSearchCodes.Category(category.Name),
                        CategoryText(category),
                        (IEnumerable<RoomInfoSnapshot>)categoryRooms[i]
                    )
            ),
        ];

        foreach (var (code, title, rooms) in sections)
        {
            var matching = rooms
                .Where(x => MatchesText(x, text))
                .Take(PreviewLimit(code) + 1)
                .ToList();

            if (matching.Count > 0)
                blocks.Add(CreatePreviewBlock(query, code, title, matching));
        }

        return [.. blocks];
    }

    /// <summary>
    /// The "show more" of a block of the filtered hotel view (official client capture
    /// 2026-10-16, "trade"): one block, action back, of the query's matches (75) or of the
    /// section's own full list filtered by the text (Trading: its 12 matches, empty rooms
    /// included); null for any other code.
    /// </summary>
    private async Task<NavigatorSearchResultBlockSnapshot?> CreateFilteredSectionBlockAsync(
        SearchQuery query,
        string code,
        NavigatorSearchFilterType filterType,
        string value,
        CancellationToken ct
    )
    {
        if (code == NavigatorSearchCodes.QUERY)
        {
            var matches = await SearchByFilterAsync(query, filterType, value, ct)
                .ConfigureAwait(false);

            return CreateBlock(
                query,
                code,
                string.Empty,
                [.. matches.OrderByDescending(x => PopulationOf(x, query))],
                NavigatorActionAllowedType.Back,
                _config.SearchResultLimits.GetValueOrDefault(code, _config.SearchResultLimit)
            );
        }

        if (
            filterType != NavigatorSearchFilterType.Anything
            || (
                code != NavigatorSearchCodes.RECOMMENDED
                && !code.StartsWith(NavigatorSearchCodes.CATEGORY_PREFIX, StringComparison.Ordinal)
            )
        )
            return null;

        var block = (
            await CreateSectionBlockAsync(query, code, ct).ConfigureAwait(false)
        ).FirstOrDefault();
        var text = value.Trim();

        return block is null
            ? null
            : block with
            {
                Results = [.. block.Results.Where(x => MatchesText(x, text))],
            };
    }

    /// <summary>
    /// The in-memory form of the room search for unprefixed text: the room's name contains it,
    /// or its owner or one of its tags is it.
    /// </summary>
    private static bool MatchesText(RoomInfoSnapshot room, string text) =>
        room.Name.Contains(text, StringComparison.OrdinalIgnoreCase)
        || string.Equals(room.OwnerName, text, StringComparison.OrdinalIgnoreCase)
        || (
            !room.Tags.IsDefault
            && room.Tags.Any(tag => string.Equals(tag, text, StringComparison.OrdinalIgnoreCase))
        );

    private int PreviewLimit(string code) =>
        _config.BlockPreviewLimits.TryGetValue(code, out var limit) ? limit
        : code.StartsWith(NavigatorSearchCodes.EVENT_CATEGORY_PREFIX, StringComparison.Ordinal)
            ? _config.EventCategoryBlockPreviewLimit
        : _config.BlockPreviewLimit;

    /// <summary>
    /// A preview block of the rooms fetched with one to spare: the spare one tells the client
    /// there is more to show.
    /// </summary>
    private NavigatorSearchResultBlockSnapshot CreatePreviewBlock(
        SearchQuery query,
        string code,
        string text,
        List<RoomInfoSnapshot> rooms
    )
    {
        var limit = PreviewLimit(code);

        return CreateBlock(
            query,
            code,
            text,
            rooms,
            rooms.Count > limit
                ? NavigatorActionAllowedType.Expanded
                : NavigatorActionAllowedType.Collapsed,
            limit
        );
    }

    /// <summary>
    /// A category block's title as Habbo sends it: a category with a global key is titled by
    /// its text (<c>${navigator.flatcategory.global.GAMES}</c>), any other by its name
    /// (official client capture 2026-10-16).
    /// </summary>
    private static string CategoryText(NavigatorFlatCategorySnapshot category) =>
        string.IsNullOrEmpty(category.GlobalCategoryKey)
            ? category.Name
            : $"${{navigator.flatcategory.global.{category.GlobalCategoryKey}}}";

    /// <summary>
    /// Rooms Habbo lists in its hotel view: occupied, with an open door, fullest first.
    /// </summary>
    private static IEnumerable<RoomActiveSnapshot> ListedRooms(SearchQuery query) =>
        query
            .LiveRooms.Values.Where(x => x.Population > 0 && IsOpen(x))
            .OrderByDescending(x => x.Population)
            .ThenByDescending(x => x.Score)
            .ThenBy(x => x.RoomId.Value);

    /// <summary>
    /// The rooms players are entering now, most recent visitors first. Habbo's hot rooms are
    /// not its fullest: across three samples 32 minutes apart they were led by the rooms that
    /// had just filled up (one went from 24 to 75 users and moved to the top), so ordering by
    /// recent entries is inference from that.
    /// </summary>
    private static List<RoomInfoSnapshot> GetHotRooms(SearchQuery query, int limit) =>
        [
            .. ListedRooms(query)
                .Where(x => x.RecentVisitors > 0)
                .OrderByDescending(x => x.RecentVisitors)
                .Take(limit),
        ];

    /// <summary>
    /// Habbo's recommended rooms: the same few rooms in three samples 32 minutes apart, the
    /// occupied ones with open doors, fullest first (official client capture 2026-10-16). Which
    /// rooms it picks from is its own, so the pool is the configured categories.
    /// </summary>
    private List<RoomInfoSnapshot> GetRecommendedRooms(SearchQuery query, int limit) =>
        [
            .. ListedRooms(query)
                .Where(x =>
                    _config.RecommendedCategoryIds.Length == 0
                    || _config.RecommendedCategoryIds.Contains(x.CategoryId)
                )
                .Take(limit),
        ];

    /// <summary>
    /// Habbo's Public tab (official client capture 2026-10-13 .. 10-18, seven samples): one block
    /// per visible category in the order staff set, code <c>official_&lt;id&gt;</c>, the name as
    /// its text, no "show more" (action 0), the category's forced close and view mode (thumbnails on
    /// all six), its rooms in the curated order with doorbell and invisible ones. A category with
    /// no rooms is left out (inference: the hotel view drops its empty blocks; Habbo never had one).
    /// </summary>
    private async Task<ImmutableArray<NavigatorSearchResultBlockSnapshot>> CreatePublicBlocksAsync(
        SearchQuery query,
        CancellationToken ct
    )
    {
        var categories = await GetVisiblePublicCategoriesAsync(ct).ConfigureAwait(false);
        var rooms = await Task.WhenAll(categories.Select(x => GetPublicRoomsAsync(query, x, ct)))
            .ConfigureAwait(false);

        return
        [
            .. categories
                .Select((category, i) => (Category: category, Rooms: rooms[i]))
                .Where(x => x.Rooms.Count > 0)
                .Select(x => CreatePublicBlock(query, x.Category, x.Rooms)),
        ];
    }

    /// <summary>
    /// One Public tab block searched by its code (a <c>navigator/tab/&lt;code&gt;</c> link): the
    /// block as the tab has it (inference: Habbo's answer was not recorded). <c>official</c>, the
    /// saved search "Public Rooms", is one untitled list block with a back action (official
    /// client capture 2026-10-26). Null when the code is no visible category.
    /// </summary>
    private async Task<NavigatorSearchResultBlockSnapshot?> CreatePublicSectionBlockAsync(
        SearchQuery query,
        string code,
        CancellationToken ct
    )
    {
        if (code == NavigatorSearchCodes.OFFICIAL)
            return new()
            {
                SearchCode = code,
                Text = string.Empty,
                ActionAllowed = NavigatorActionAllowedType.Back,
                Localization = string.Empty,
                ForceClosed = false,
                ViewMode = NavigatorViewModeType.Rows,
                Results =
                [
                    .. (await GetOpenPublicRoomsAsync(query, ct).ConfigureAwait(false))
                        .Take(_config.SearchResultLimit)
                        .Select(x => ToSearchResult(x, query)),
                ],
            };

        if (
            code != NavigatorSearchCodes.STAFF_PICKS
            && NavigatorSearchCodes.GetOfficialCategoryId(code) is null
        )
            return null;

        var category = (
            await GetVisiblePublicCategoriesAsync(ct).ConfigureAwait(false)
        ).FirstOrDefault(x => x.SearchCode == code);

        return category is null
            ? null
            : CreatePublicBlock(
                query,
                category,
                await GetPublicRoomsAsync(query, category, ct).ConfigureAwait(false)
            );
    }

    private NavigatorSearchResultBlockSnapshot CreatePublicBlock(
        SearchQuery query,
        NavigatorPublicCategorySnapshot category,
        List<RoomInfoSnapshot> rooms
    ) =>
        new()
        {
            SearchCode = category.SearchCode,
            Text = category.Name,
            ActionAllowed = NavigatorActionAllowedType.Collapsed,
            Localization = string.Empty,
            ForceClosed = category.ForceClosed,
            ViewMode = category.ViewMode,
            Results =
            [
                .. rooms.Take(_config.SearchResultLimit).Select(x => ToSearchResult(x, query)),
            ],
        };

    private async Task<
        ImmutableArray<NavigatorPublicCategorySnapshot>
    > GetVisiblePublicCategoriesAsync(CancellationToken ct) =>
        [
            .. (await _navigatorProvider.GetPublicCategoriesAsync(ct).ConfigureAwait(false)).Where(
                x => x.Visible
            ),
        ];

    /// <summary>
    /// A Public tab category's rooms: the staff-picked ones newest pick first, or the ones staff
    /// put in it in their order. A room Builders Club hides stays its owner's alone.
    /// </summary>
    private async Task<List<RoomInfoSnapshot>> GetPublicRoomsAsync(
        SearchQuery query,
        NavigatorPublicCategorySnapshot category,
        CancellationToken ct
    )
    {
        var limit = _config.SearchResultLimit;
        IEnumerable<RoomInfoSnapshot> rooms;

        if (category.Kind == NavigatorPublicCategoryKind.StaffPicks)
            rooms = MergeLive(
                await _navigatorProvider.GetStaffPickedRoomsAsync(limit, ct).ConfigureAwait(false),
                query,
                x => x.StaffPick
            );
        else
            rooms = await ResolveRoomsAsync(query, [.. category.RoomIds.Take(limit)], ct)
                .ConfigureAwait(false);

        return [.. rooms.Where(x => !x.HiddenByBc).Take(limit)];
    }

    /// <summary>Every room of the Public tab in its order, each once: the hotel's public rooms.</summary>
    private async Task<List<RoomInfoSnapshot>> GetAllPublicRoomsAsync(
        SearchQuery query,
        CancellationToken ct
    )
    {
        var categories = await GetVisiblePublicCategoriesAsync(ct).ConfigureAwait(false);
        var rooms = await Task.WhenAll(categories.Select(x => GetPublicRoomsAsync(query, x, ct)))
            .ConfigureAwait(false);

        return [.. rooms.SelectMany(x => x).DistinctBy(x => x.RoomId)];
    }

    /// <summary>
    /// The public rooms a player can walk into, busiest first, then by name (official client
    /// capture 2026-10-26: 31 of the tab's 34 rooms, the doorbell and invisible ones left out).
    /// Inference from those 31 rows: names compare lower-cased by code point, so "-- Island Port"
    /// and "[BaW] ..." come before "Ambassador Hub" and "The Ditch" before "Theatre".
    /// </summary>
    private async Task<List<RoomInfoSnapshot>> GetOpenPublicRoomsAsync(
        SearchQuery query,
        CancellationToken ct
    ) =>
        [
            .. (await GetAllPublicRoomsAsync(query, ct).ConfigureAwait(false))
                .Where(IsOpen)
                .OrderByDescending(x => PopulationOf(x, query))
                .ThenBy(x => x.Name.ToLowerInvariant(), StringComparer.Ordinal)
                .ThenBy(x => x.RoomId.Value),
        ];

    private async Task<ImmutableArray<NavigatorSearchResultBlockSnapshot>> CreateSectionBlockAsync(
        SearchQuery query,
        string code,
        CancellationToken ct
    )
    {
        var limit =
            _config.SearchResultLimits.TryGetValue(code, out var codeLimit) ? codeLimit
            : code.StartsWith(NavigatorSearchCodes.CATEGORY_PREFIX, StringComparison.Ordinal)
                ? _config.CategorySearchResultLimit
            : _config.SearchResultLimit;
        var rooms = await GetSectionRoomsAsync(query, code, limit, ct).ConfigureAwait(false);

        if (rooms is null)
            return [];

        var text = code.StartsWith(
            NavigatorSearchCodes.EVENT_CATEGORY_PREFIX,
            StringComparison.Ordinal
        )
            ? FindEventCategory(code)?.Name ?? NavigatorSearchCodes.GetCategoryName(code)
            : await GetCategoryTextAsync(query.PlayerId, code, ct).ConfigureAwait(false);

        return [CreateBlock(query, code, text, rooms, NavigatorActionAllowedType.Back, limit)];
    }

    /// <summary>
    /// A full category search keeps the title its hotel view block had (inference: Habbo's
    /// "show more" of a category was not recorded); any other code is untitled.
    /// </summary>
    private async Task<string> GetCategoryTextAsync(
        PlayerId playerId,
        string code,
        CancellationToken ct
    )
    {
        var name = NavigatorSearchCodes.GetCategoryName(code);

        if (name.Length == 0)
            return name;

        var category = (
            await GetFlatCategoriesForPlayerAsync(playerId, ct).ConfigureAwait(false)
        ).FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));

        return category is null ? name : CategoryText(category);
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
            // Not the player's collapsed blocks: the client applies those itself and opens the hot
            // rooms in the hotel view anyway (BlockResultsView.applyHotelViewExpansionDefaults),
            // which a forced close would override. Habbo sent false on every block of a player
            // whose collapsed list starts with hot (official client capture 2026-10-13).
            ForceClosed = false,
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
                return
                [
                    .. (await GetAllPublicRoomsAsync(query, ct).ConfigureAwait(false)).Take(limit),
                ];
            case NavigatorSearchCodes.OFFICIAL:
                return
                [
                    .. (await GetOpenPublicRoomsAsync(query, ct).ConfigureAwait(false)).Take(limit),
                ];
            case NavigatorSearchCodes.HOT:
                return GetHotRooms(query, limit);
            case NavigatorSearchCodes.POPULAR:
                return await GetPopularRoomsAsync(query, limit, ct).ConfigureAwait(false);
            case NavigatorSearchCodes.RECOMMENDED:
                return GetRecommendedRooms(query, limit);
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
                // The new navigator already holds the player's navigator snapshot; only a
                // legacy search has to ask for it.
                var roomIds =
                    query.Preferences?.FavouriteRoomIds
                    ?? await GetFavouriteRoomIdsAsync(playerId, ct).ConfigureAwait(false);

                return await GetRoomsInOrderAsync(query, roomIds, limit, ct).ConfigureAwait(false);
            }
            case NavigatorSearchCodes.HISTORY:
            {
                var roomIds = await _grainFactory
                    .GetPlayerNavigatorGrain(playerId)
                    .GetRecentRoomIdsAsync(limit, ct)
                    .ConfigureAwait(false);

                return await GetRoomsInOrderAsync(query, roomIds, limit, ct).ConfigureAwait(false);
            }
            case NavigatorSearchCodes.FREQUENT_HISTORY:
            {
                var roomIds = await _grainFactory
                    .GetPlayerNavigatorGrain(playerId)
                    .GetFrequentRoomIdsAsync(limit, ct)
                    .ConfigureAwait(false);

                return await GetRoomsInOrderAsync(query, roomIds, limit, ct).ConfigureAwait(false);
            }
            case NavigatorSearchCodes.FRIENDS_ROOMS:
            {
                var friends = await query.GetFriendsAsync(_grainFactory, ct).ConfigureAwait(false);
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
                var roomIds = await GetRoomsWithOnlineFriendsAsync(query, ct).ConfigureAwait(false);

                return await GetRoomsInOrderAsync(query, roomIds, limit, ct).ConfigureAwait(false);
            }
            case NavigatorSearchCodes.WITH_RIGHTS:
            {
                // Rights are not part of the live copy, so active rooms are only refreshed.
                var cached = await _navigatorProvider
                    .GetRoomsWithRightsAsync(playerId, _config.SearchResultLimit, ct)
                    .ConfigureAwait(false);

                return
                [
                    .. MergeLive(cached, query, include: null)
                        .Where(x => IsListedForRightsHolder(x, playerId))
                        .Take(limit),
                ];
            }
            case NavigatorSearchCodes.MY_GROUPS:
            {
                var roomIds = await GetMyGuildBaseRoomIdsAsync(playerId, ct).ConfigureAwait(false);

                return await GetRoomsInOrderAsync(query, roomIds, limit, ct).ConfigureAwait(false);
            }
            case NavigatorSearchCodes.GROUPS:
                return await GetGuildBaseRoomsAsync(query, limit, ct).ConfigureAwait(false);
            case NavigatorSearchCodes.TOP_PROMOTIONS:
            {
                var rooms = await GetEventRoomsAsync(query, null, _config.SearchResultLimit, ct)
                    .ConfigureAwait(false);

                // Habbo's top promotions run longest first: both samples (official client
                // capture 2026-10-13) are in falling minutes left (9301, 4499, 2868, ... 546),
                // whatever their population.
                return
                [
                    .. rooms
                        .OrderByDescending(x => x.ActiveEvent!.ExpiresAtUtc)
                        .ThenByDescending(x => x.RoomId.Value)
                        .Take(limit),
                ];
            }
            case NavigatorSearchCodes.NEW_ADS:
                return await GetEventRoomsAsync(query, null, limit, ct).ConfigureAwait(false);
        }

        if (code.StartsWith(NavigatorSearchCodes.CATEGORY_PREFIX, StringComparison.Ordinal))
        {
            var name = NavigatorSearchCodes.GetCategoryName(code);
            var category = (
                await GetFlatCategoriesForPlayerAsync(playerId, ct).ConfigureAwait(false)
            ).FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));

            return category is null
                ? []
                : await GetCategoryRoomsAsync(query, category.Id, limit, ct).ConfigureAwait(false);
        }

        if (code.StartsWith(NavigatorSearchCodes.EVENT_CATEGORY_PREFIX, StringComparison.Ordinal))
        {
            var category = FindEventCategory(code);

            return category is null
                ? []
                : await GetEventRoomsAsync(query, category.Id, limit, ct).ConfigureAwait(false);
        }

        return null;
    }

    /// <summary>
    /// The event category an <c>eventcategory__</c> code names: by id, as Habbo's blocks do, or
    /// by name, as its collapsed list and older saved searches do.
    /// </summary>
    private NavigatorEventCategorySnapshot? FindEventCategory(string code)
    {
        var key = NavigatorSearchCodes.GetCategoryName(code);
        var categories = GetEventCategories();

        return (
                int.TryParse(key, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id)
                    ? categories.FirstOrDefault(x => x.Id == id)
                    : null
            )
            ?? categories.FirstOrDefault(x =>
                string.Equals(x.Name, key, StringComparison.OrdinalIgnoreCase)
            );
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
                .TryConsumeSearchQuotaAsync(ct)
                .ConfigureAwait(false)
        )
            return [];

        var cached = await _navigatorProvider
            .SearchRoomsAsync(filterType, value, _config.SearchResultLimit, ct)
            .ConfigureAwait(false);

        return [.. MergeLive(cached, query, include: null).Where(IsPublic)];
    }

    /// <summary>
    /// Occupied open rooms by population, straight from the directory: Habbo's popular rooms
    /// (official client capture 2026-10-16: 12 in the hotel view and 75 in the full search, all
    /// occupied and open). With <see cref="NavigatorConfig.PopularFillWithTopRated"/> a quiet
    /// hotel's list is topped up with the best-rated rooms.
    /// </summary>
    private async Task<List<RoomInfoSnapshot>> GetPopularRoomsAsync(
        SearchQuery query,
        int limit,
        CancellationToken ct
    )
    {
        var rooms = ListedRooms(query).Take(limit).Cast<RoomInfoSnapshot>().ToList();

        if (rooms.Count >= limit || !_config.PopularFillWithTopRated)
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

    /// <summary>
    /// A category's open rooms, fullest first, then the best-rated empty ones: Habbo's "show
    /// more" of a category (official client capture 2026-10-16: 75 rooms, all open, 69 occupied
    /// by users then score, then 6 empty by score).
    /// </summary>
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
            .. MergeLive(cached, query, x => x.CategoryId == categoryId && IsOpen(x))
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
        return
        [
            .. (await ResolveRoomsAsync(query, roomIds, ct).ConfigureAwait(false))
                .Where(x => IsVisibleTo(x, query.PlayerId))
                .Take(limit),
        ];
    }

    /// <summary>Rooms by id in the given order: active rooms from the live copy, the rest from the room cache.</summary>
    private async Task<List<RoomInfoSnapshot>> ResolveRoomsAsync(
        SearchQuery query,
        IReadOnlyCollection<RoomId> roomIds,
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
                .OfType<RoomInfoSnapshot>(),
        ];
    }

    private async Task<List<RoomId>> GetRoomsWithOnlineFriendsAsync(
        SearchQuery query,
        CancellationToken ct
    )
    {
        var friends = await query.GetFriendsAsync(_grainFactory, ct).ConfigureAwait(false);

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
        room.ListedDoorMode != RoomDoorModeType.Invisible;

    private static bool IsOpen(RoomInfoSnapshot room) =>
        room.DoorMode == RoomDoorModeType.Open && !room.HiddenByBc;

    private static bool IsVisibleTo(RoomInfoSnapshot room, PlayerId playerId) =>
        IsPublic(room) || room.OwnerId == playerId;

    /// <summary>
    /// A room Builders Club hides is its owner's alone, rights or not; a room its owner made
    /// invisible still shows to those with rights, as the setting's own text says.
    /// </summary>
    private static bool IsListedForRightsHolder(RoomInfoSnapshot room, PlayerId playerId) =>
        !room.HiddenByBc || room.OwnerId == playerId;

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
    )
    {
        private readonly Lock _friendsLock = new();
        private Task<List<MessengerFriendDto>>? _friends;

        /// <summary>
        /// The player's friends, asked of their messenger once per request however many
        /// sections want them (a preview of "my world" wants them twice, concurrently).
        /// </summary>
        public Task<List<MessengerFriendDto>> GetFriendsAsync(
            IGrainFactory grainFactory,
            CancellationToken ct
        )
        {
            lock (_friendsLock)
                return _friends ??= grainFactory
                    .GetPlayerMessengerGrain(PlayerId)
                    .GetFriendsAsync(ct);
        }
    }
}
