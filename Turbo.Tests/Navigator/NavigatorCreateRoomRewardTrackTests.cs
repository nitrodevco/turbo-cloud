using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Navigator;
using Turbo.Navigator.Configuration;
using Turbo.Players.Configuration;
using Turbo.Primitives.Moderation;
using Turbo.Primitives.Navigator;
using Turbo.Primitives.Navigator.Snapshots;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Players.Providers;
using Turbo.Primitives.Players.Snapshots.Permissions;
using Turbo.Primitives.Quests;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Navigator;

/// <summary>
/// A room created counts for a <c>create_room</c> reward track task (the official client's
/// <c>reward_track_tasks_create_room</c> image, JS 88); a refused creation does not.
/// </summary>
public sealed class NavigatorCreateRoomRewardTrackTests
{
    private const int PLAYER = 1;

    private readonly Fakes _fakes = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public NavigatorCreateRoomRewardTrackTests()
    {
        var registry = new PermissionRegistry([new CorePermissionNodeSource()]);

        _fakes.Handlers["get_Current"] = call =>
            call.Interface == typeof(IPermissionRegistryProvider) ? registry : Fakes.NotHandled;
        _fakes.Handlers["GetResolvedAsync"] = _ =>
            Task.FromResult(ResolvedPermissionsSnapshot.EMPTY);
        _fakes.Handlers["GetListingViewAsync"] = _ =>
            Task.FromResult(
                new RoomListingViewSnapshot
                {
                    ActiveRooms = [],
                    Epoch = Guid.Empty,
                    Sequence = 0,
                    ChangedKeys = [],
                    IsReset = false,
                }
            );
        _fakes.Handlers["GetRoomCountForOwnerAsync"] = _ => Task.FromResult(0);
        _fakes.Handlers["GetFlatCategories"] = _ =>
            System.Collections.Immutable.ImmutableArray<NavigatorFlatCategorySnapshot>.Empty;
        _fakes.Handlers["GetRoomModelIdByNameAsync"] = call =>
            Task.FromResult<int?>((string)call.Args[0]! == "model_a" ? 1 : null);
        _fakes.Handlers["CreateRoomAsync"] = _ => Task.FromResult((RoomId)9);
        _fakes.Handlers["FilterAndTruncate"] = call => (string)call.Args[0]!;
    }

    [Fact]
    public async Task A_room_created_counts_and_a_refused_one_does_not()
    {
        var service = Service();

        (await CreateAsync(service, "model_a")).Should().Be((RoomId)9);
        (await CreateAsync(service, "no_such_model")).Should().BeNull();

        _fakes
            .Log.Of("RecordActionAsync")
            .Select(x => x.Args[0])
            .Should()
            .Equal(RewardTrackActionTypes.CREATE_ROOM);
    }

    private static Task<RoomId?> CreateAsync(NavigatorService service, string model) =>
        service.CreateRoomAsync(
            PLAYER,
            "my room",
            "",
            model,
            0,
            25,
            RoomTradeModeType.Disabled,
            Ct
        );

    private NavigatorService Service() =>
        new(
            NullLogger<INavigatorService>.Instance,
            _fakes.Create<INavigatorProvider>(),
            _fakes.Create<IGrainFactory>(),
            Options.Create(new NavigatorConfig()),
            Options.Create(new PlayerNavigatorConfig()),
            _fakes.Create<IPermissionRegistryProvider>(),
            _fakes.Create<IWordFilter>()
        );
}
