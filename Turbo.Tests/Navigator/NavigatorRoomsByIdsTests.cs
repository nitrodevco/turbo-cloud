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
using Turbo.Primitives.Players.Providers;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Rooms.Configuration;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Navigator;

/// <summary>
/// Rooms by id for a caller outside the navigator (the pets tab names the room each placed pet
/// stands in): a loaded room as it is now, since a name changed while it is loaded is not in the
/// room cache yet, the rest from the cache, and nothing for a room that is gone.
/// </summary>
public sealed class NavigatorRoomsByIdsTests
{
    private readonly Fakes _fakes = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_loaded_room_is_read_live_and_the_rest_from_the_room_cache()
    {
        _fakes.Handlers["GetListingViewAsync"] = _ =>
            Task.FromResult(
                new RoomListingViewSnapshot
                {
                    ActiveRooms = [new RoomActiveSnapshot(Room(10, "Renamed just now"))],
                    Epoch = Guid.Empty,
                    Sequence = 0,
                    ChangedKeys = [],
                    IsReset = false,
                }
            );
        // Room 12 was deleted.
        _fakes.Handlers["GetRoomsByIdsAsync"] = call =>
            Task.FromResult(
                ((IReadOnlyCollection<RoomId>)call.Args[0]!)
                    .Where(x => x.Value is 10 or 11)
                    .Select(x => Room(x.Value, $"cached {x.Value}"))
                    .ToList()
            );

        var rooms = await Service().GetRoomsByIdsAsync([new(10), new(11), new(12), new(10)], Ct);

        rooms
            .ToDictionary(x => x.Key.Value, x => x.Value.Name)
            .Should()
            .Equal(new Dictionary<int, string> { [10] = "Renamed just now", [11] = "cached 11" });
        ((IReadOnlyCollection<RoomId>)_fakes.Log.Of("GetRoomsByIdsAsync").Single().Args[0]!)
            .Should()
            .Equal(new RoomId(11), new RoomId(12));
    }

    [Fact]
    public async Task No_ids_ask_nothing()
    {
        (await Service().GetRoomsByIdsAsync([], Ct)).Should().BeEmpty();

        _fakes.Log.Calls.Should().BeEmpty();
    }

    private NavigatorService Service() =>
        new(
            NullLogger<INavigatorService>.Instance,
            _fakes.Create<INavigatorProvider>(),
            _fakes.Create<IGrainFactory>(),
            Options.Create(new NavigatorConfig()),
            Options.Create(new RoomConfig()),
            Options.Create(new PlayerNavigatorConfig()),
            _fakes.Create<IPermissionRegistryProvider>(),
            _fakes.Create<IWordFilter>(),
            Turbo.Players.Policies.GamePolicies.None
        );

    private static RoomInfoSnapshot Room(int roomId, string name) =>
        ((RoomInfoSnapshot)RuntimeHelpers.GetUninitializedObject(typeof(RoomInfoSnapshot))) with
        {
            RoomId = roomId,
            Name = name,
        };
}
