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

    private NavigatorService Service()
    {
        var provider = _fakes.Create<INavigatorProvider>();

        return new NavigatorService(
            NullLogger<INavigatorService>.Instance,
            provider,
            _fakes.Create<IGrainFactory>(),
            Options.Create(new NavigatorConfig()),
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
