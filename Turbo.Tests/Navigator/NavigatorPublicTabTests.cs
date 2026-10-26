using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Navigator;
using Turbo.Navigator.Configuration;
using Turbo.Players.Configuration;
using Turbo.Players.Policies;
using Turbo.Primitives.Moderation;
using Turbo.Primitives.Navigator;
using Turbo.Primitives.Navigator.Enums;
using Turbo.Primitives.Navigator.Snapshots;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Players.Providers;
using Turbo.Primitives.Players.Snapshots.Navigator;
using Turbo.Primitives.Players.Snapshots.Permissions;
using Turbo.Primitives.Policies;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Navigator;

/// <summary>
/// The navigator's Public tab (<c>official_view</c>) as Habbo sends it, from the official client
/// capture <c>2026-10-18T04-35-07-click-wait-shot-click-wait-shot-click-wait-shot-.jsonl</c>
/// (NavigatorSearchResultBlocks at t=855, the same in all seven samples 2026-10-13..18): six
/// curated categories, each block <c>official_&lt;id&gt;</c> with its literal name, action 0,
/// its own forced close and thumbnails, the rooms in the order staff set (Coffee House, 22 users,
/// sixth of nine), doorbell and invisible rooms included, a room in several blocks. Staff picks
/// are the room flag, listed only through a staff picks category.
/// </summary>
public sealed class NavigatorPublicTabTests
{
    private const int PLAYER = 1;
    private const int OWNER = 2;

    private readonly Fakes _fakes = new();
    private readonly Dictionary<RoomId, RoomActiveSnapshot> _live = [];
    private readonly Dictionary<RoomId, RoomInfoSnapshot> _rooms = [];
    private readonly List<RoomInfoSnapshot> _staffPicks = [];
    private ImmutableArray<NavigatorPublicCategorySnapshot> _categories = [];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // The recorded blocks: code id, name, forceClosed, rooms (id, name, door) in their order.
    // Habbo's "New" listed [Habbox] Rooms Hallway! twice; a room is in a category once here.
    private static readonly (
        int Id,
        string Name,
        bool ForceClosed,
        (int Id, string Name, RoomDoorModeType Door)[] Rooms
    )[] Recorded =
    [
        (
            273411,
            "New",
            false,
            [
                (80465265, "[SPS] Lobby", RoomDoorModeType.Open),
                (80463193, "Witches' Sabbath Bundle", RoomDoorModeType.Open),
                (78291407, "[Habbox] Rooms Hallway!", RoomDoorModeType.Locked),
                (80447067, "World Deaf Day 2026", RoomDoorModeType.Open),
                (79036967, "[Ducket] The Hub", RoomDoorModeType.Open),
                (80435574, "Sunset Ranch: The Adventure Begins", RoomDoorModeType.Open),
                (80434059, "[BaW] Aspen Acres", RoomDoorModeType.Open),
                (80356969, "[FUUSIO.co] Fansite Lounge", RoomDoorModeType.Open),
                (80434055, ".com BaW Hub", RoomDoorModeType.Invisible),
                (80430745, "Toast Valley", RoomDoorModeType.Open),
                (80411891, "[TDM] Habham lobby", RoomDoorModeType.Open),
                (80395090, "[SIC] Summer in Colors – Hall", RoomDoorModeType.Open),
                (80295928, "BAW Collab 2026 - Super Habbo World [Entrance]", RoomDoorModeType.Open),
            ]
        ),
        (
            273420,
            "Official Rooms",
            false,
            [
                (69118542, "Welcome Lounge", RoomDoorModeType.Open),
                (79384010, "The Collectors Guild", RoomDoorModeType.Open),
                (69118541, "Club NX", RoomDoorModeType.Open),
                (70616899, "Theatre", RoomDoorModeType.Open),
                (70616906, "Picnic", RoomDoorModeType.Open),
                (69118545, "Coffee House", RoomDoorModeType.Open),
                (70170123, "Hall of Habbo", RoomDoorModeType.Open),
                (69118543, "HC Lounge", RoomDoorModeType.Open),
                (80434055, ".com BaW Hub", RoomDoorModeType.Invisible),
            ]
        ),
        (
            273425,
            "Games",
            true,
            [
                (80435574, "Sunset Ranch: The Adventure Begins", RoomDoorModeType.Open),
                (80430745, "Toast Valley", RoomDoorModeType.Open),
                (80411891, "[TDM] Habham lobby", RoomDoorModeType.Open),
                (80395090, "[SIC] Summer in Colors – Hall", RoomDoorModeType.Open),
                (80346590, "[CDH] Connect the Dots!", RoomDoorModeType.Open),
                (80295928, "BAW Collab 2026 - Super Habbo World [Entrance]", RoomDoorModeType.Open),
                (80198387, "Wired 15th Anniversary - Entrance", RoomDoorModeType.Open),
                (78686087, "Habbo Wired Tutorial", RoomDoorModeType.Open),
                (77638342, "Snowstorm Lobby", RoomDoorModeType.Open),
                (75211764, "Habbo Inside © - Flappy Bird [REBUILD]", RoomDoorModeType.Open),
            ]
        ),
        (
            273430,
            "Room Bundles",
            true,
            [(80463193, "Witches' Sabbath Bundle", RoomDoorModeType.Open)]
        ),
        (
            273434,
            "Our Fansites",
            true,
            [
                (79075182, "-- Island Port", RoomDoorModeType.Open),
                (80356969, "[FUUSIO.co] Fansite Lounge", RoomDoorModeType.Open),
                (79036967, "[Ducket] The Hub", RoomDoorModeType.Open),
                (78291407, "[Habbox] Rooms Hallway!", RoomDoorModeType.Locked),
                (77832609, "[HxHD] Habbox Help Desk", RoomDoorModeType.Open),
            ]
        ),
        (
            273438,
            "Help & Safety",
            true,
            [
                (76880729, "Ambassador Hub", RoomDoorModeType.Open),
                (78190651, "SID 2022 - Start Room", RoomDoorModeType.Open),
                (77703937, "Frank's Lobby I", RoomDoorModeType.Open),
                (76952863, "The Ditch the Label 2020 Hub", RoomDoorModeType.Open),
                (78236752, "Ambassador InfoBus Park", RoomDoorModeType.Open),
                (75389840, "Tour of Habbo", RoomDoorModeType.Open),
            ]
        ),
    ];

    public NavigatorPublicTabTests()
    {
        var registry = new PermissionRegistry([new CorePermissionNodeSource()]);

        _fakes.Handlers["get_Current"] = call =>
            call.Interface == typeof(IPermissionRegistryProvider) ? registry : Fakes.NotHandled;
        _fakes.Handlers["GetResolvedAsync"] = _ =>
            Task.FromResult(ResolvedPermissionsSnapshot.EMPTY);
        _fakes.Handlers["GetFlatCategories"] = _ =>
            (ImmutableArray<NavigatorFlatCategorySnapshot>)[];
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
                    CollapsedSearchCodes = [],
                    // The player's own list choice does not change a Public block (inference
                    // until lab L3: Habbo sent thumbnails on every block for both accounts).
                    ViewModes = ImmutableDictionary<string, NavigatorViewModeType>.Empty.Add(
                        "official_273411",
                        NavigatorViewModeType.Rows
                    ),
                }
            );
        _fakes.Handlers["GetPublicCategoriesAsync"] = _ => Task.FromResult(_categories);
        _fakes.Handlers["GetRoomsByIdsAsync"] = call =>
            Task.FromResult(
                ((IReadOnlyCollection<RoomId>)call.Args[0]!)
                    .Where(_rooms.ContainsKey)
                    .Select(x => _rooms[x])
                    .ToList()
            );
        _fakes.Handlers["GetStaffPickedRoomsAsync"] = _ => Task.FromResult(_staffPicks.ToList());

        foreach (var (_, _, _, rooms) in Recorded)
        foreach (var (id, name, door) in rooms)
            _rooms[id] = Room(id, name, door);

        // Coffee House full, as recorded (22 users, score 3619): still sixth.
        _live[69118545] = RoomActiveSnapshot.From(_rooms[69118545] with { Score = 3619 }, 22);

        _categories =
        [
            StaffPicksCategory(visible: false),
            .. Recorded.Select(
                (x, i) =>
                    Category(x.Id, x.Name, i + 1, x.ForceClosed, [.. x.Rooms.Select(r => r.Id)])
            ),
        ];
    }

    [Fact]
    public async Task official_view_sends_each_category_as_habbos_block_in_the_curated_order()
    {
        var blocks = await Service().SearchAsync(PLAYER, "official_view", string.Empty, Ct);

        blocks
            .Select(x =>
                (
                    x.SearchCode,
                    x.Text,
                    x.ActionAllowed,
                    x.ForceClosed,
                    x.ViewMode,
                    string.Join(',', x.Results.Select(r => r.RoomId.Value))
                )
            )
            .Should()
            .Equal(
                Recorded.Select(x =>
                    (
                        $"official_{x.Id}",
                        x.Name,
                        NavigatorActionAllowedType.Collapsed,
                        x.ForceClosed,
                        NavigatorViewModeType.Tiles,
                        string.Join(',', x.Rooms.Select(r => r.Id))
                    )
                )
            );
        blocks[1].Results[5].Population.Should().Be(22);
    }

    [Fact]
    public async Task staff_picks_show_only_through_a_visible_staff_picks_category_newest_pick_first()
    {
        _staffPicks.AddRange([
            Picked(501, RoomDoorModeType.Open),
            Picked(500, RoomDoorModeType.Invisible),
        ]);

        var hidden = await Service().SearchAsync(PLAYER, "official_view", string.Empty, Ct);

        hidden.Select(x => x.SearchCode).Should().NotContain("staffpicks");
        (await Service().SearchAsync(PLAYER, "staffpicks", string.Empty, Ct)).Should().BeEmpty();

        _categories = _categories.SetItem(0, StaffPicksCategory(visible: true));

        var shown = await Service().SearchAsync(PLAYER, "official_view", string.Empty, Ct);

        // Untitled, so the client titles it navigator.searchcode.title.staffpicks ("Staff Picks").
        shown[0]
            .Should()
            .BeEquivalentTo(
                new
                {
                    SearchCode = "staffpicks",
                    Text = "",
                    ActionAllowed = NavigatorActionAllowedType.Collapsed,
                }
            );
        shown[0].Results.Select(x => x.RoomId.Value).Should().Equal(501, 500);
        Assert
            .Single(await Service().SearchAsync(PLAYER, "staffpicks", string.Empty, Ct))
            .Results.Should()
            .HaveCount(2);
    }

    [Fact]
    public async Task an_empty_or_hidden_category_sends_no_block()
    {
        _categories =
        [
            Category(1, "Empty", 1, false, []),
            Category(2, "Hidden", 2, false, [69118545], visible: false),
            Category(3, "Shown", 3, false, [69118545]),
        ];

        (await Service().SearchAsync(PLAYER, "official_view", string.Empty, Ct))
            .Select(x => x.SearchCode)
            .Should()
            .Equal("official_3");
        (await Service().SearchAsync(PLAYER, "official_2", string.Empty, Ct)).Should().BeEmpty();
    }

    [Fact]
    public async Task a_category_searched_by_its_code_is_its_block()
    {
        var games = Assert.Single(
            await Service().SearchAsync(PLAYER, "official_273425", string.Empty, Ct)
        );

        games.Text.Should().Be("Games");
        games.ForceClosed.Should().BeTrue();
        games.Results.Should().HaveCount(10);
    }

    /// <summary>
    /// The saved search "Public Rooms" (<c>official</c>), official client capture
    /// <c>2026-10-26T15-13-22-click-wait-shot.jsonl</c> t=900, a minute after the Public tab of
    /// <c>2026-10-26T15-12-01-click-wait-click-wait-shot.jsonl</c> t=6335 (the categories below,
    /// users from the 15:13 answer): one untitled list block with action 2, the tab's 34 distinct
    /// rooms minus its doorbell room and two invisible ones, busiest first and then by name.
    /// </summary>
    [Fact]
    public async Task official_is_one_list_block_of_the_open_public_rooms_busiest_first_then_by_name()
    {
        const RoomDoorModeType open = RoomDoorModeType.Open;
        (
            int Id,
            bool ForceClosed,
            (int Id, string Name, RoomDoorModeType Door, int Users)[]
        )[] tab =
        [
            (
                273731,
                false,
                [
                    (80497945, "Haunted Library Bundle 1/2", open, 2),
                    (80465265, "[SPS] Lobby", open, 12),
                    (78291407, "[Habbox] Rooms Hallway!", open, 0),
                    (80447067, "World Deaf Day 2026", open, 0),
                    (79036967, "[Ducket] The Hub", open, 1),
                    (80434059, "[BaW] Aspen Acres", open, 0),
                    (80356969, "[FUUSIO.co] Fansite Lounge", RoomDoorModeType.Locked, 0),
                    (80434055, ".com BaW Hub", RoomDoorModeType.Invisible, 0),
                    // Habbo listed [Habbox] Rooms Hallway! here again; a category has a room once.
                    (80430745, "Toast Valley", open, 0),
                    (80411891, "[TDM] Habham lobby", open, 1),
                    (80395090, "[SIC] Summer in Colors – Hall", open, 0),
                    (80295928, "BAW Collab 2026 - Super Habbo World [Entrance]", open, 0),
                ]
            ),
            (
                273738,
                false,
                [
                    (69118542, "Welcome Lounge", open, 0),
                    (79384010, "The Collectors Guild", open, 0),
                    (69118541, "Club NX", open, 0),
                    (70616899, "Theatre", open, 0),
                    (70616906, "Picnic", open, 2),
                    (69118545, "Coffee House", open, 34),
                    (70170123, "Hall of Habbo", open, 0),
                    (69118543, "HC Lounge", open, 0),
                    (80434055, ".com BaW Hub", RoomDoorModeType.Invisible, 0),
                ]
            ),
            (
                273741,
                true,
                [
                    (80430745, "Toast Valley", open, 0),
                    (80411891, "[TDM] Habham lobby", open, 1),
                    (80395090, "[SIC] Summer in Colors – Hall", open, 0),
                    (80346590, "[CDH] Connect the Dots!", open, 0),
                    (80295928, "BAW Collab 2026 - Super Habbo World [Entrance]", open, 0),
                    (80198387, "Wired 15th Anniversary - Entrance", open, 0),
                    (78686087, "Habbo Wired Tutorial", open, 0),
                    (77638342, "Snowstorm Lobby", open, 0),
                    (75211764, "Habbo Inside © - Flappy Bird [REBUILD]", open, 0),
                ]
            ),
            (273751, true, [(80463193, "Witches' Sabbath Bundle", RoomDoorModeType.Invisible, 0)]),
            (
                273753,
                true,
                [
                    (79075182, "-- Island Port", open, 0),
                    (80356969, "[FUUSIO.co] Fansite Lounge", RoomDoorModeType.Locked, 0),
                    (79036967, "[Ducket] The Hub", open, 1),
                    (78291407, "[Habbox] Rooms Hallway!", open, 0),
                    (77832609, "[HxHD] Habbox Help Desk", open, 0),
                ]
            ),
            (
                273757,
                true,
                [
                    (76880729, "Ambassador Hub", open, 0),
                    (78190651, "SID 2022 - Start Room", open, 0),
                    (77703937, "Frank's Lobby I", open, 0),
                    (76952863, "The Ditch the Label 2020 Hub", open, 0),
                    (78236752, "Ambassador InfoBus Park", open, 0),
                    (75389840, "Tour of Habbo", open, 0),
                ]
            ),
        ];

        _rooms.Clear();
        _live.Clear();

        foreach (var (_, _, rooms) in tab)
        foreach (var (id, name, door, users) in rooms)
        {
            _rooms[id] = Room(id, name, door);

            if (users > 0)
                _live[id] = RoomActiveSnapshot.From(_rooms[id], users);
        }

        _categories =
        [
            StaffPicksCategory(visible: false),
            .. tab.Select(
                (x, i) =>
                    Category(
                        x.Id,
                        $"block {i}",
                        i + 1,
                        x.ForceClosed,
                        [.. x.Item3.Select(r => r.Id)]
                    )
            ),
        ];

        var block = Assert.Single(
            await Service().SearchAsync(PLAYER, "official", string.Empty, Ct)
        );

        block.SearchCode.Should().Be("official");
        block.Text.Should().BeEmpty();
        block.ActionAllowed.Should().Be(NavigatorActionAllowedType.Back);
        block.ForceClosed.Should().BeFalse();
        block.ViewMode.Should().Be(NavigatorViewModeType.Rows);
        block
            .Results.Select(x => x.RoomId.Value)
            .Should()
            .Equal(
                69118545, // Coffee House, 34 users
                80465265, // [SPS] Lobby, 12
                80497945, // Haunted Library Bundle 1/2, 2
                70616906, // Picnic, 2
                79036967, // [Ducket] The Hub, 1
                80411891, // [TDM] Habham lobby, 1
                79075182, // -- Island Port
                80434059, // [BaW] Aspen Acres
                80346590, // [CDH] Connect the Dots!
                78291407, // [Habbox] Rooms Hallway!
                77832609, // [HxHD] Habbox Help Desk
                80395090, // [SIC] Summer in Colors – Hall
                76880729, // Ambassador Hub
                78236752, // Ambassador InfoBus Park
                80295928, // BAW Collab 2026 - Super Habbo World [Entrance]
                69118541, // Club NX
                77703937, // Frank's Lobby I
                75211764, // Habbo Inside © - Flappy Bird [REBUILD]
                78686087, // Habbo Wired Tutorial
                70170123, // Hall of Habbo
                69118543, // HC Lounge
                78190651, // SID 2022 - Start Room
                77638342, // Snowstorm Lobby
                79384010, // The Collectors Guild
                76952863, // The Ditch the Label 2020 Hub
                70616899, // Theatre
                80430745, // Toast Valley
                75389840, // Tour of Habbo
                69118542, // Welcome Lounge
                80198387, // Wired 15th Anniversary - Entrance
                80447067 // World Deaf Day 2026
            );
    }

    /// <summary>
    /// A collapsed Public tab block is not kept (official client capture
    /// <c>2026-10-26T15-18-40-NewNavigatorInit-wait.jsonl</c>: after two collapses of
    /// <c>official_273731</c> Habbo's collapsed list had no <c>official_</c> code); others are.
    /// </summary>
    [Fact]
    public async Task collapsing_a_public_tab_block_is_not_stored()
    {
        await Service().AddCollapsedSearchCodeAsync(PLAYER, "official_273731", Ct);
        await Service().AddCollapsedSearchCodeAsync(PLAYER, "hot", Ct);

        _fakes.Log.Of("AddCollapsedSearchCodeAsync").Select(x => x.Args[0]).Should().Equal("hot");
    }

    [Fact]
    public async Task the_hotels_public_rooms_are_the_rooms_of_its_visible_categories_not_the_staff_picks()
    {
        _staffPicks.Add(Picked(500, RoomDoorModeType.Open));
        _categories =
        [
            StaffPicksCategory(visible: false),
            Category(1, "Shown", 1, false, [69118545, 80434055]),
            Category(2, "Hidden", 2, false, [70616899], visible: false),
        ];

        (await Service().GetOfficialRoomsAsync(Ct))
            .Select(x => x.RoomId.Value)
            .Should()
            .Equal(69118545, 80434055);
    }

    /// <summary>
    /// Not Habbo's: a plugin's <see cref="IPublicRoomOrder"/> (here the plugin template's
    /// busiest-first one) orders a block's rooms by the players in them now; the rest keep the
    /// order staff set, as with no policy (the golden test above).
    /// </summary>
    [Fact]
    public async Task a_plugins_public_room_order_lists_a_category_busiest_first()
    {
        _live[70616899] = RoomActiveSnapshot.From(_rooms[70616899], 5);
        var policies = GamePolicies.None;
        using var registered = policies.Register("sample", new BusiestFirst());

        var blocks = await Service(policies).SearchAsync(PLAYER, "official_view", string.Empty, Ct);

        blocks[1]
            .Results.Select(x => (x.RoomId.Value, x.Population))
            .Should()
            .Equal(
                (69118545, 22), // Coffee House
                (70616899, 5), // Theatre
                (69118542, 0),
                (79384010, 0),
                (69118541, 0),
                (70616906, 0),
                (70170123, 0),
                (69118543, 0),
                (80434055, 0)
            );
        blocks[0]
            .Results.Select(x => x.RoomId.Value)
            .Should()
            .Equal(Recorded[0].Rooms.Select(x => x.Id));
    }

    /// <summary>A policy only orders: a room it names that the block lacks is not listed, one it leaves out follows in its place before.</summary>
    [Fact]
    public async Task a_public_room_order_cannot_add_or_drop_a_room()
    {
        _categories = [Category(1, "Shown", 1, false, [69118545, 70616899, 69118542])];
        var policies = GamePolicies.None;
        using var registered = policies.Register("sample", new LastOnlyPlusStranger());

        var blocks = await Service(policies).SearchAsync(PLAYER, "official_view", string.Empty, Ct);

        blocks
            .Single()
            .Results.Select(x => x.RoomId.Value)
            .Should()
            .Equal(69118542, 69118545, 70616899);
    }

    public sealed class BusiestFirst : IPublicRoomOrder
    {
        public IReadOnlyList<RoomInfoSnapshot> OrderRooms(
            PublicRoomCategory category,
            IReadOnlyList<RoomInfoSnapshot> rooms
        ) => [.. rooms.OrderByDescending(x => x.Population)];
    }

    public sealed class LastOnlyPlusStranger : IPublicRoomOrder
    {
        public IReadOnlyList<RoomInfoSnapshot> OrderRooms(
            PublicRoomCategory category,
            IReadOnlyList<RoomInfoSnapshot> rooms
        ) => [rooms[^1] with { RoomId = 424242 }, rooms[^1]];
    }

    private NavigatorService Service(GamePolicies? policies = null) =>
        new(
            NullLogger<INavigatorService>.Instance,
            _fakes.Create<INavigatorProvider>(),
            _fakes.Create<IGrainFactory>(),
            Options.Create(new NavigatorConfig()),
            Options.Create(new PlayerNavigatorConfig()),
            _fakes.Create<IPermissionRegistryProvider>(),
            _fakes.Create<IWordFilter>(),
            policies ?? GamePolicies.None
        );

    private static NavigatorPublicCategorySnapshot Category(
        int id,
        string name,
        int order,
        bool forceClosed,
        ImmutableArray<RoomId> rooms,
        bool visible = true
    ) =>
        new()
        {
            Id = id,
            Name = name,
            OrderNum = order,
            ForceClosed = forceClosed,
            ViewMode = NavigatorViewModeType.Tiles,
            Visible = visible,
            Kind = NavigatorPublicCategoryKind.Manual,
            RoomIds = rooms,
        };

    private static NavigatorPublicCategorySnapshot StaffPicksCategory(bool visible) =>
        new()
        {
            Id = 1000,
            Name = string.Empty,
            OrderNum = 0,
            ForceClosed = false,
            ViewMode = NavigatorViewModeType.Tiles,
            Visible = visible,
            Kind = NavigatorPublicCategoryKind.StaffPicks,
            RoomIds = [],
        };

    private static RoomInfoSnapshot Room(int roomId, string name, RoomDoorModeType door) =>
        Uninitialized<RoomInfoSnapshot>() with
        {
            RoomId = roomId,
            Name = name,
            OwnerId = OWNER,
            DoorMode = door,
            CategoryId = 1,
            StaffPick = false,
            Tags = [],
        };

    private static RoomInfoSnapshot Picked(int roomId, RoomDoorModeType door) =>
        Room(roomId, $"picked {roomId}", door) with
        {
            StaffPick = true,
        };

    private static T Uninitialized<T>() => (T)RuntimeHelpers.GetUninitializedObject(typeof(T));
}
