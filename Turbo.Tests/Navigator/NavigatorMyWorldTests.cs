using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Navigator;
using Turbo.Navigator.Configuration;
using Turbo.Players.Configuration;
using Turbo.Primitives.Guilds.Snapshots;
using Turbo.Primitives.Moderation;
using Turbo.Primitives.Navigator;
using Turbo.Primitives.Navigator.Enums;
using Turbo.Primitives.Navigator.Snapshots;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Messenger;
using Turbo.Primitives.Players.Providers;
using Turbo.Primitives.Players.Snapshots.Navigator;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Navigator;

/// <summary>
/// The navigator's My World tab, as Habbo shows it: My Rooms, My Favorite Rooms, My Groups, My
/// Room Visit History, Rooms with my friends, My Friends' Rooms, Rooms Where I Have Rights,
/// Frequently Visited Rooms. The client titles a block from its code
/// (<c>navigator.searchcode.title.&lt;code&gt;</c>), and the player's own groups are
/// <c>my_groups</c>; <c>groups</c> is the hotel's biggest groups, which the group window's
/// "show groups" link asks for (<c>performGuildBaseSearch</c>).
/// </summary>
public sealed class NavigatorMyWorldTests
{
    private const int PLAYER = 1;
    private const int FRIEND = 2;
    private const int STRANGER = 3;

    private readonly Fakes _fakes = new();
    private readonly Dictionary<RoomId, RoomActiveSnapshot> _live = [];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public NavigatorMyWorldTests()
    {
        // One room in every section, so each block is sent.
        Live(1, PLAYER);
        Live(2, STRANGER);
        Live(3, STRANGER);
        Live(4, STRANGER);
        Live(5, STRANGER);
        Live(6, FRIEND);
        Live(7, STRANGER);
        Live(8, STRANGER);
        Live(9, STRANGER);

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
                    FavouriteRoomIds = [2],
                    SavedSearches = [],
                    CollapsedSearchCodes = [],
                    ViewModes = ImmutableDictionary<string, NavigatorViewModeType>.Empty,
                }
            );
        _fakes.Handlers["GetMembershipsAsync"] = _ =>
            Task.FromResult<ImmutableArray<GuildInfoSnapshot>>([
                Uninitialized<GuildInfoSnapshot>() with
                {
                    GroupId = 30,
                },
            ]);
        _fakes.Handlers["GetSummariesAsync"] = _ =>
            Task.FromResult<ImmutableArray<GuildSummarySnapshot>>([
                Uninitialized<GuildSummarySnapshot>() with
                {
                    RoomId = 3,
                },
            ]);
        _fakes.Handlers["GetGuildBaseRoomIdsAsync"] = _ =>
            Task.FromResult<ImmutableArray<RoomId>>([9]);
        _fakes.Handlers["GetRecentRoomIdsAsync"] = _ =>
            Task.FromResult<ImmutableArray<RoomId>>([4]);
        _fakes.Handlers["GetFrequentRoomIdsAsync"] = _ =>
            Task.FromResult<ImmutableArray<RoomId>>([8]);
        _fakes.Handlers["GetFriendsAsync"] = _ =>
            Task.FromResult(
                new List<MessengerFriendDto>
                {
                    Uninitialized<MessengerFriendDto>() with
                    {
                        PlayerId = FRIEND,
                        Online = true,
                    },
                }
            );
        _fakes.Handlers["GetActiveRoomAsync"] = _ =>
            Task.FromResult(
                new RoomPointerSnapshot { RoomId = 5, ActiveSinceUtc = DateTime.UtcNow }
            );
        _fakes.Handlers["GetRoomsByOwnersAsync"] = _ =>
            Task.FromResult(new List<RoomInfoSnapshot>());
        _fakes.Handlers["GetRoomsWithRightsAsync"] = _ =>
            Task.FromResult(new List<RoomInfoSnapshot> { _live[7] });
        _fakes.Handlers["GetRoomsByIdsAsync"] = _ => Task.FromResult(new List<RoomInfoSnapshot>());
    }

    [Fact]
    public async Task my_world_lists_its_blocks_in_habbos_order()
    {
        var blocks = await Service()
            .SearchAsync(PLAYER, NavigatorSearchCodes.MYWORLD_VIEW, string.Empty, Ct);

        blocks
            .Select(x => x.SearchCode)
            .Should()
            .Equal(
                "my",
                "favorites",
                "my_groups",
                "history",
                "with_friends",
                "friends_rooms",
                "with_rights",
                "history_freq"
            );
    }

    [Fact]
    public async Task my_world_keeps_favorites_while_empty_as_habbo_does()
    {
        // Habbo's My World for a player with no favourites: my, favorites (0 rooms), history,
        // history_freq; the other empty blocks are left out.
        _fakes.Handlers["GetSnapshotAsync"] = _ =>
            Task.FromResult(
                new PlayerNavigatorSnapshot
                {
                    FavouriteRoomIds = [],
                    SavedSearches = [],
                    CollapsedSearchCodes = [],
                    ViewModes = ImmutableDictionary<string, NavigatorViewModeType>.Empty,
                }
            );
        _fakes.Handlers["GetRoomsWithRightsAsync"] = _ =>
            Task.FromResult(new List<RoomInfoSnapshot>());

        var blocks = await Service()
            .SearchAsync(PLAYER, NavigatorSearchCodes.MYWORLD_VIEW, string.Empty, Ct);

        blocks
            .Select(x => x.SearchCode)
            .Should()
            .Contain("favorites")
            .And.NotContain("with_rights");
        blocks.Single(x => x.SearchCode == "favorites").Results.Should().BeEmpty();

        var none = await Service(new NavigatorConfig { MyWorldKeepEmpty = [""] })
            .SearchAsync(PLAYER, NavigatorSearchCodes.MYWORLD_VIEW, string.Empty, Ct);

        none.Select(x => x.SearchCode).Should().NotContain("favorites");
    }

    [Fact]
    public async Task event_blocks_are_named_by_category_id_as_habbo_does()
    {
        // Habbo's Events tab: eventcategory__2 (titled by the client from
        // navigator.searchcode.title.eventcategory__2), not eventcategory__<name>.
        var party = Uninitialized<RoomInfoSnapshot>() with
        {
            RoomId = 20,
            Name = "party",
            OwnerId = STRANGER,
            DoorMode = RoomDoorModeType.Open,
            Tags = [],
            ActiveEvent = new RoomEventSnapshot
            {
                EventId = 1,
                RoomId = 20,
                OwnerId = STRANGER,
                OwnerName = "stranger",
                CategoryId = 2,
                Name = "party",
                Description = "",
                CreatedAtUtc = DateTime.UtcNow,
                ExpiresAtUtc = DateTime.UtcNow.AddHours(1),
            },
        };
        _fakes.Handlers["GetEventCategories"] = _ =>
            (ImmutableArray<NavigatorEventCategorySnapshot>)
                [
                    new()
                    {
                        Id = 2,
                        Name = "Parties & Music",
                        Visible = true,
                    },
                ];
        _fakes.Handlers["GetRoomsWithActiveEventsAsync"] = _ =>
            Task.FromResult(new List<RoomInfoSnapshot> { party });

        var blocks = await Service()
            .SearchAsync(PLAYER, NavigatorSearchCodes.ROOMADS_VIEW, string.Empty, Ct);

        var block = blocks.Single(x => x.SearchCode.StartsWith("eventcategory__"));
        block.SearchCode.Should().Be("eventcategory__2");
        // Habbo's event blocks carry no text (official client capture 2026-10-13).
        block.Text.Should().BeEmpty();

        foreach (var code in new[] { "eventcategory__2", "eventcategory__Parties & Music" })
            Assert
                .Single(await Service().SearchAsync(PLAYER, code, string.Empty, Ct))
                .Results.Select(x => x.RoomId.Value)
                .Should()
                .Equal(20);
    }

    [Fact]
    public async Task the_events_tab_has_habbos_blocks_sizes_and_promotion_order()
    {
        // Habbo's roomads_view (official client capture 2026-10-13, two samples): top_promotions
        // 8, longest running first whatever the population; eventcategory__<id> 6 each; no
        // new_ads block.
        var now = DateTime.UtcNow;
        var rooms = Enumerable
            .Range(1, 10)
            .Select(i =>
                Uninitialized<RoomInfoSnapshot>() with
                {
                    RoomId = 100 + i,
                    Name = $"event {i}",
                    OwnerId = STRANGER,
                    DoorMode = RoomDoorModeType.Open,
                    Population = i,
                    Tags = [],
                    ActiveEvent = new RoomEventSnapshot
                    {
                        EventId = i,
                        RoomId = 100 + i,
                        OwnerId = STRANGER,
                        OwnerName = "stranger",
                        CategoryId = 2,
                        Name = $"event {i}",
                        Description = "",
                        CreatedAtUtc = now.AddMinutes(-i),
                        // Listed 101 first, but room 110 runs longest.
                        ExpiresAtUtc = now.AddMinutes(900 + (i * 10)),
                    },
                }
            )
            .ToList();
        _fakes.Handlers["GetEventCategories"] = _ =>
            (ImmutableArray<NavigatorEventCategorySnapshot>)
                [
                    new()
                    {
                        Id = 2,
                        Name = "Parties & Music",
                        Visible = true,
                    },
                ];
        _fakes.Handlers["GetRoomsWithActiveEventsAsync"] = _ => Task.FromResult(rooms.ToList());

        var blocks = await Service()
            .SearchAsync(PLAYER, NavigatorSearchCodes.ROOMADS_VIEW, string.Empty, Ct);

        blocks
            .Select(x => (x.SearchCode, x.Results.Length, x.ActionAllowed))
            .Should()
            .Equal(
                ("top_promotions", 8, NavigatorActionAllowedType.Expanded),
                ("eventcategory__2", 6, NavigatorActionAllowedType.Expanded)
            );
        blocks[0]
            .Results.Select(x => x.RoomId.Value)
            .Should()
            .Equal(110, 109, 108, 107, 106, 105, 104, 103);
    }

    [Fact]
    public async Task my_groups_lists_the_homerooms_of_the_players_own_groups()
    {
        var block = Assert.Single(
            await Service().SearchAsync(PLAYER, "my_groups", string.Empty, Ct)
        );

        block.SearchCode.Should().Be("my_groups");
        block.Results.Select(x => x.RoomId.Value).Should().Equal(3);
    }

    [Fact]
    public async Task groups_lists_the_hotels_biggest_groups()
    {
        var block = Assert.Single(await Service().SearchAsync(PLAYER, "groups", string.Empty, Ct));

        block.SearchCode.Should().Be("groups");
        block.Results.Select(x => x.RoomId.Value).Should().Equal(9);
    }

    private NavigatorService Service(NavigatorConfig? config = null)
    {
        var provider = _fakes.Create<INavigatorProvider>();

        return new NavigatorService(
            NullLogger<INavigatorService>.Instance,
            provider,
            _fakes.Create<IGrainFactory>(),
            Options.Create(config ?? new NavigatorConfig()),
            Options.Create(new PlayerNavigatorConfig()),
            _fakes.Create<IPermissionRegistryProvider>(),
            _fakes.Create<IWordFilter>()
        );
    }

    private void Live(int roomId, int ownerId) =>
        _live[roomId] = RoomActiveSnapshot.From(
            Uninitialized<RoomInfoSnapshot>() with
            {
                RoomId = roomId,
                Name = $"room {roomId}",
                OwnerId = ownerId,
                DoorMode = RoomDoorModeType.Open,
                Tags = [],
            },
            population: 1
        );

    private static T Uninitialized<T>() => (T)RuntimeHelpers.GetUninitializedObject(typeof(T));
}
