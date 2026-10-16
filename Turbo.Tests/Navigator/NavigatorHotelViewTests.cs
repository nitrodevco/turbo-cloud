using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Navigator;
using Turbo.Navigator.Configuration;
using Turbo.Players.Configuration;
using Turbo.Primitives.Moderation;
using Turbo.Primitives.Navigator;
using Turbo.Primitives.Navigator.Enums;
using Turbo.Primitives.Navigator.Snapshots;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Players.Providers;
using Turbo.Primitives.Players.Snapshots.Navigator;
using Turbo.Primitives.Players.Snapshots.Permissions;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Navigator;

/// <summary>
/// The navigator's hotel view, as Habbo sends it (official client capture 2026-10-13 and
/// 2026-10-16, six samples): <c>hot</c> 12, <c>popular</c> 12, <c>recommended</c> 7, then
/// <c>category__&lt;name&gt;</c> 9 each, every room occupied and open, empty blocks left out. A
/// category with a global key is titled <c>${navigator.flatcategory.global.&lt;key&gt;}</c>.
/// </summary>
public sealed class NavigatorHotelViewTests
{
    private const int PLAYER = 1;
    private const int OWNER = 2;
    private const int GAMES = 3;
    private const int TRADING = 14;
    private const int HELP = 6;

    private readonly Fakes _fakes = new();
    private readonly Dictionary<RoomId, RoomActiveSnapshot> _live = [];
    private readonly List<string> _collapsed = [];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public NavigatorHotelViewTests()
    {
        var registry = new PermissionRegistry([new CorePermissionNodeSource()]);

        _fakes.Handlers["get_Current"] = call =>
            call.Interface == typeof(IPermissionRegistryProvider) ? registry : Fakes.NotHandled;
        _fakes.Handlers["GetResolvedAsync"] = _ =>
            Task.FromResult(ResolvedPermissionsSnapshot.EMPTY);
        _fakes.Handlers["GetFlatCategories"] = _ =>
            (ImmutableArray<NavigatorFlatCategorySnapshot>)
                [
                    Category(GAMES, "global games", "GAMES"),
                    Category(TRADING, "Trading", ""),
                    Category(HELP, "global help", "HELP"),
                ];
        _fakes.Handlers["GetListingViewAsync"] = _ =>
            Task.FromResult(
                new RoomListingViewSnapshot
                {
                    ActiveRooms = [.. _live.Values],
                    Epoch = Guid.Empty,
                    Sequence = 0,
                    ChangedKeys = [],
                    IsReset = false,
                }
            );
        _fakes.Handlers["GetSnapshotAsync"] = _ =>
            Task.FromResult(
                new PlayerNavigatorSnapshot
                {
                    FavouriteRoomIds = [],
                    SavedSearches = [],
                    CollapsedSearchCodes = [.. _collapsed],
                    ViewModes = ImmutableDictionary<string, NavigatorViewModeType>.Empty,
                }
            );
        _fakes.Handlers["GetHighestScoredRoomsAsync"] = _ =>
            Task.FromResult(new List<RoomInfoSnapshot>());

        // 20 occupied open games rooms (1..20 users), 5 occupied open trading rooms, a full
        // doorbell room and a full password room, and empty open rooms in help.
        for (var i = 1; i <= 20; i++)
            Live(i, GAMES, population: i, recentVisitors: 21 - i);

        for (var i = 1; i <= 5; i++)
            Live(100 + i, TRADING, population: i);

        Live(200, GAMES, population: 50, recentVisitors: 50, door: RoomDoorModeType.Locked);
        Live(201, TRADING, population: 50, recentVisitors: 50, door: RoomDoorModeType.Password);
        Live(300, HELP, population: 0);
    }

    [Fact]
    public async Task hotel_view_sends_habbos_blocks_in_habbos_order_and_sizes()
    {
        var blocks = await Service().SearchAsync(PLAYER, "hotel_view", string.Empty, Ct);

        blocks
            .Select(x => (x.SearchCode, x.Results.Length, x.ActionAllowed))
            .Should()
            .Equal(
                ("hot", 12, NavigatorActionAllowedType.Expanded),
                ("popular", 12, NavigatorActionAllowedType.Expanded),
                ("recommended", 7, NavigatorActionAllowedType.Expanded),
                ("category__global games", 9, NavigatorActionAllowedType.Expanded),
                ("category__Trading", 5, NavigatorActionAllowedType.Collapsed)
            );
    }

    [Fact]
    public async Task hotel_view_lists_occupied_open_rooms_only()
    {
        var blocks = await Service().SearchAsync(PLAYER, "hotel_view", string.Empty, Ct);
        var listed = blocks.SelectMany(x => x.Results).Select(x => x.RoomId.Value).ToHashSet();

        listed.Should().NotContain([200, 201, 300]);
        blocks.Select(x => x.SearchCode).Should().NotContain("category__global help");
    }

    [Fact]
    public async Task popular_is_fullest_first_and_hot_is_most_recent_visitors_first()
    {
        var blocks = await Service().SearchAsync(PLAYER, "hotel_view", string.Empty, Ct);

        blocks
            .Single(x => x.SearchCode == "popular")
            .Results.Select(x => x.RoomId.Value)
            .Should()
            .StartWith([20, 19, 18, 17]);
        blocks
            .Single(x => x.SearchCode == "hot")
            .Results.Select(x => x.RoomId.Value)
            .Should()
            .StartWith([1, 2, 3]);
    }

    [Fact]
    public async Task category_blocks_are_titled_as_habbo_titles_them()
    {
        var blocks = await Service().SearchAsync(PLAYER, "hotel_view", string.Empty, Ct);

        blocks
            .Single(x => x.SearchCode == "category__global games")
            .Text.Should()
            .Be("${navigator.flatcategory.global.GAMES}");
        blocks.Single(x => x.SearchCode == "category__Trading").Text.Should().Be("Trading");
    }

    [Fact]
    public async Task collapsed_blocks_are_left_to_the_client_rather_than_forced_closed()
    {
        // Habbo's player had hot (and every category) collapsed, yet no hotel view block came
        // force-closed; the client opens hot itself when the hotel view has it.
        _collapsed.AddRange(["hot", "popular", "category__global games"]);

        var blocks = await Service().SearchAsync(PLAYER, "hotel_view", string.Empty, Ct);

        blocks.Should().OnlyContain(x => !x.ForceClosed);
    }

    [Fact]
    public async Task recommended_draws_from_the_configured_categories()
    {
        var blocks = await Service(new NavigatorConfig { RecommendedCategoryIds = [TRADING] })
            .SearchAsync(PLAYER, "hotel_view", string.Empty, Ct);

        blocks
            .Single(x => x.SearchCode == "recommended")
            .Results.Select(x => x.RoomId.Value)
            .Should()
            .Equal(105, 104, 103, 102, 101);
    }

    [Fact]
    public async Task the_full_popular_search_returns_75_occupied_open_rooms()
    {
        for (var i = 1; i <= 80; i++)
            Live(1000 + i, TRADING, population: 1);

        var block = Assert.Single(await Service().SearchAsync(PLAYER, "popular", string.Empty, Ct));

        block.Results.Should().HaveCount(75);
        block.ActionAllowed.Should().Be(NavigatorActionAllowedType.Back);
        block.Results.Select(x => x.RoomId.Value).Should().NotContain([200, 201]);
    }

    [Fact]
    public async Task a_categorys_show_more_lists_75_open_rooms_fullest_then_best_rated()
    {
        // Habbo's category__Trading: 75 open rooms, occupied by users, then empty by score.
        var empty = Enumerable
            .Range(1, 80)
            .Select(i => Room(2000 + i, TRADING, 0, i))
            .Append(Room(3000, TRADING, 0, 999, RoomDoorModeType.Locked))
            .ToList();
        _fakes.Handlers["GetRoomsByCategoryAsync"] = _ => Task.FromResult(empty);

        var block = Assert.Single(
            await Service().SearchAsync(PLAYER, "category__Trading", string.Empty, Ct)
        );

        block.Text.Should().Be("Trading");
        block.ActionAllowed.Should().Be(NavigatorActionAllowedType.Back);
        block
            .Results.Select(x => x.RoomId.Value)
            .Should()
            .HaveCount(75)
            .And.StartWith([105, 104, 103, 102, 101, 2080, 2079])
            .And.NotContain([201, 3000]);
    }

    [Fact]
    public async Task the_full_hot_and_recommended_searches_answer()
    {
        // Every open games room had recent visitors; the trading rooms had none.
        Assert
            .Single(await Service().SearchAsync(PLAYER, "hot", string.Empty, Ct))
            .Results.Should()
            .HaveCount(20);
        Assert
            .Single(await Service().SearchAsync(PLAYER, "recommended", string.Empty, Ct))
            .Results.Should()
            .HaveCount(25);
    }

    [Fact]
    public async Task text_in_the_hotel_view_answers_a_query_block_then_the_matching_blocks()
    {
        // Habbo's "room linker" / "trade" (official client capture 2026-10-12): query (every
        // match, doorbell and password rooms too, fullest first), then recommended and the
        // categories with their matching rooms only; no hot or popular.
        SearchMatches();

        var blocks = await Service().SearchAsync(PLAYER, "hotel_view", "room 20", Ct);

        blocks
            .Select(x => (x.SearchCode, x.ActionAllowed))
            .Should()
            .Equal(
                ("query", NavigatorActionAllowedType.Collapsed),
                ("recommended", NavigatorActionAllowedType.Collapsed),
                ("category__global games", NavigatorActionAllowedType.Collapsed)
            );
        blocks[0].Results.Select(x => x.RoomId.Value).Should().Equal(201, 200, 20);
        blocks[0].Text.Should().BeEmpty();
        blocks[1].Results.Select(x => x.RoomId.Value).Should().Equal(20);
        blocks[2].Results.Select(x => x.RoomId.Value).Should().Equal(20);
    }

    [Fact]
    public async Task the_filtered_hotel_views_blocks_have_habbos_sizes()
    {
        // query 12, recommended 7, categories 9 ("room linker", "trade"); a category's block
        // keeps the empty rooms its full list ends with (Habbo's Trading block for "trade").
        SearchMatches();

        var blocks = await Service().SearchAsync(PLAYER, "hotel_view", "room", Ct);

        blocks
            .Select(x => (x.SearchCode, x.Results.Length, x.ActionAllowed))
            .Should()
            .Equal(
                ("query", 12, NavigatorActionAllowedType.Expanded),
                ("recommended", 7, NavigatorActionAllowedType.Expanded),
                ("category__global games", 9, NavigatorActionAllowedType.Expanded),
                ("category__Trading", 5, NavigatorActionAllowedType.Collapsed),
                ("category__global help", 1, NavigatorActionAllowedType.Collapsed)
            );
        blocks[0].Results.Select(x => x.RoomId.Value).Should().StartWith([201, 200, 20, 19]);
    }

    [Fact]
    public async Task a_prefixed_filter_in_the_hotel_view_answers_the_query_block_alone()
    {
        // Habbo's "owner:results!" (official client capture 2026-10-13): query only.
        SearchMatches();

        var blocks = await Service().SearchAsync(PLAYER, "hotel_view", "roomname:room 2", Ct);

        blocks.Select(x => x.SearchCode).Should().Equal("query");
    }

    [Fact]
    public async Task show_more_of_a_filtered_category_lists_its_matching_rooms()
    {
        SearchMatches();

        var block = Assert.Single(
            await Service().SearchAsync(PLAYER, "category__global games", "room 2", Ct)
        );

        block.ActionAllowed.Should().Be(NavigatorActionAllowedType.Back);
        block.Results.Select(x => x.RoomId.Value).Should().Equal(20, 2);
    }

    /// <summary>The room search: rooms whose name has the text, best-rated (here newest) first.</summary>
    private void SearchMatches()
    {
        _fakes.Handlers["TryConsumeSearchQuotaAsync"] = _ => Task.FromResult(true);
        _fakes.Handlers["GetRoomsByCategoryAsync"] = _ =>
            Task.FromResult(new List<RoomInfoSnapshot>());
        _fakes.Handlers["SearchRoomsAsync"] = call =>
            Task.FromResult(
                _live
                    .Values.Where(x =>
                        x.Name.Contains((string)call.Args[1]!, StringComparison.OrdinalIgnoreCase)
                    )
                    .OrderByDescending(x => x.RoomId.Value)
                    .Cast<RoomInfoSnapshot>()
                    .ToList()
            );
    }

    private NavigatorService Service(NavigatorConfig? config = null) =>
        new(
            NullLogger<INavigatorService>.Instance,
            _fakes.Create<INavigatorProvider>(),
            _fakes.Create<IGrainFactory>(),
            Options.Create(config ?? new NavigatorConfig()),
            Options.Create(new PlayerNavigatorConfig()),
            _fakes.Create<IPermissionRegistryProvider>(),
            _fakes.Create<IWordFilter>()
        );

    private void Live(
        int roomId,
        int categoryId,
        int population,
        int recentVisitors = 0,
        RoomDoorModeType door = RoomDoorModeType.Open
    ) =>
        _live[roomId] = RoomActiveSnapshot.From(
            Room(roomId, categoryId, population, score: 0, door),
            population,
            recentVisitors
        );

    private static RoomInfoSnapshot Room(
        int roomId,
        int categoryId,
        int population,
        int score,
        RoomDoorModeType door = RoomDoorModeType.Open
    ) =>
        Uninitialized<RoomInfoSnapshot>() with
        {
            RoomId = roomId,
            Name = $"room {roomId}",
            OwnerId = OWNER,
            DoorMode = door,
            CategoryId = categoryId,
            Population = population,
            Score = score,
            Tags = [],
        };

    private static NavigatorFlatCategorySnapshot Category(int id, string name, string key) =>
        new()
        {
            Id = id,
            Name = name,
            Visible = true,
            Automatic = false,
            AutomaticCategoryKey = "NONE",
            GlobalCategoryKey = key,
            StaffOnly = false,
            MinRank = 1,
            OrderNum = id,
        };

    private static T Uninitialized<T>() => (T)RuntimeHelpers.GetUninitializedObject(typeof(T));
}
