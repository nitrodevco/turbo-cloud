using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Contracts.Hotel;
using Turbo.Database.Context;
using Turbo.Main.Hotel;
using Turbo.Plugins;
using Turbo.Plugins.Configuration;
using Turbo.Primitives.Navigator;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Grains;
using Turbo.Primitives.Plugins;
using Turbo.Primitives.Plugins.Messages;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Grains;
using Turbo.Primitives.Rooms.Watching;
using Turbo.Runtime.AssemblyProcessing;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Hotel;

/// <summary>
/// Plugins' holds and watches through the hotel API (docs/plugin-room-watch.md): each is the
/// plugin's own, so a plugin keeps at most its share of rooms loaded, and everything it holds or
/// has watched is let go as it unloads, without the plugin doing anything.
/// </summary>
public sealed class PluginRoomLeaseTests : IDisposable
{
    private readonly Fakes _fakes = new();
    private readonly ManualTimeProvider _clock = new(ManualTimeProvider.FixedStart);
    private readonly PluginRoomLeases _leases;
    private readonly string _folder = Path.Combine(
        Path.GetTempPath(),
        "turbo-room-leases-" + Guid.NewGuid().ToString("N")
    );

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly TimeSpan MINUTE = TimeSpan.FromMinutes(1);

    public PluginRoomLeaseTests() =>
        _leases = new PluginRoomLeases(
            _fakes.Create<IGrainFactory>(),
            _clock,
            NullLogger<PluginRoomLeases>.Instance
        );

    public void Dispose()
    {
        if (Directory.Exists(_folder))
            Directory.Delete(_folder, recursive: true);
    }

    [Fact]
    public async Task A_plugin_holds_its_share_of_rooms_and_a_lease_that_ran_out_makes_room()
    {
        for (var i = 1; i <= RoomWatchLimits.MAX_ROOMS_PER_PLUGIN; i++)
            (await _leases.HoldAsync("portals", RoomId.Parse(i), "a", MINUTE, Ct))
                .Should()
                .Be(RoomHoldOutcome.Held);

        var next = RoomId.Parse(1000);

        (await _leases.HoldAsync("portals", next, "a", MINUTE, Ct))
            .Should()
            .Be(RoomHoldOutcome.PluginFull);
        (await _leases.HoldAsync("portals", RoomId.Parse(1), "b", MINUTE, Ct))
            .Should()
            .Be(RoomHoldOutcome.Held, "another hold on a room it has is no more rooms");
        (await _leases.HoldAsync("radio", next, "a", MINUTE, Ct))
            .Should()
            .Be(RoomHoldOutcome.Held, "the share is each plugin's");
        (await _leases.WatchAsync("portals", 5, next, Envelope, MINUTE, false, Ct))
            .Should()
            .Be(RoomWatchOutcome.NotHeld, "a watch holds a room too");

        _clock.Advance(TimeSpan.FromSeconds(61));

        (await _leases.HoldAsync("portals", next, "a", MINUTE, Ct))
            .Should()
            .Be(RoomHoldOutcome.Held);
        _leases.RoomsOf("portals").Should().Be(1);
    }

    [Fact]
    public async Task A_hold_loads_the_room_first_and_a_lease_is_kept_within_its_bounds()
    {
        await _leases.HoldAsync("portals", RoomId.Parse(7), "a", TimeSpan.FromDays(1), Ct);

        _fakes
            .Log.Calls.Where(x => x.Interface == typeof(IRoomGrain))
            .Select(x => x.Method)
            .Should()
            .Equal(nameof(IRoomGrain.EnsureRoomActiveAsync), nameof(IRoomGrain.HoldAsync));
        Hold().LeaseMs.Should().Be((long)RoomWatchLimits.MAX_LEASE.TotalMilliseconds);
    }

    [Fact]
    public async Task A_room_that_cannot_load_is_not_held()
    {
        _fakes.Handlers[nameof(IRoomGrain.EnsureRoomActiveAsync)] = _ =>
            throw new InvalidOperationException("no such room");

        (await _leases.HoldAsync("portals", RoomId.Parse(7), "a", MINUTE, Ct))
            .Should()
            .Be(RoomHoldOutcome.Failed);
        _leases.RoomsOf("portals").Should().Be(0);
    }

    [Fact]
    public async Task Stopping_a_plugin_releases_the_rooms_it_still_holds_and_no_one_elses()
    {
        await _leases.HoldAsync("portals", RoomId.Parse(1), "a", MINUTE, Ct);
        await _leases.HoldAsync("portals", RoomId.Parse(2), "a", TimeSpan.FromSeconds(10), Ct);
        await _leases.WatchAsync("portals", 5, RoomId.Parse(3), Envelope, MINUTE, false, Ct);
        await _leases.HoldAsync("radio", RoomId.Parse(4), "a", MINUTE, Ct);

        _clock.Advance(TimeSpan.FromSeconds(30));
        await ((IPluginStopping)_leases).StoppingAsync("portals", Ct);

        _fakes
            .Log.Of(nameof(IRoomGrain.ReleasePluginHoldsAsync))
            .Select(x => (x.Key, x.Args[0]))
            .Should()
            .BeEquivalentTo(
                [((object)1L, (object)"portals"), (3L, "portals")],
                "room 2's lease ran out (asking would load it again), and room 4 is the radio's"
            );
        _leases.RoomsOf("portals").Should().Be(0);
        _leases.RoomsOf("radio").Should().Be(1);
    }

    /// <summary>
    /// The plugin never names itself: its container's hotel is bound to it, so a hold through
    /// <c>IHotel.Rooms</c> is the plugin's, and its unload ends it.
    /// </summary>
    [Fact]
    public async Task A_plugins_hotel_holds_rooms_as_the_plugin_and_its_unload_releases_them()
    {
        var host = new ServiceCollection()
            .AddSingleton<IHotel>(Hotel())
            .AddSingleton<IPluginStopping>(_leases)
            .BuildServiceProvider();
        var manager = new PluginManager(
            host,
            new AssemblyProcessor([]),
            Options.Create(new PluginConfig { PluginFolderPath = _folder }),
            NullLogger<PluginManager>.Instance
        );

        PluginBuilder.Write(_folder, "portals", PORTALS);
        await manager.LoadAllAsync(ct: Ct);

        var status = manager.Snapshot().Single();
        status.State.Should().Be(PluginStateType.Loaded, status.Error);
        Hold().Plugin.Should().Be("portals");
        Hold().Holder.Should().Be("lobby");

        await manager.UnloadAllAsync(Ct);

        _fakes
            .Log.Of(nameof(IRoomGrain.ReleasePluginHoldsAsync))
            .Should()
            .ContainSingle()
            .Which.Args[0]
            .Should()
            .Be("portals");
    }

    [Fact]
    public async Task A_watch_is_asked_of_the_players_presence_as_the_plugin_and_unwatched_there()
    {
        (await _leases.WatchAsync("portals", 5, RoomId.Parse(3), Envelope, MINUTE, true, Ct))
            .Should()
            .Be(RoomWatchOutcome.Watching);
        await _leases.UnwatchAsync("portals", 5, RoomId.Parse(3), Ct);

        var request = (RoomWatchRequest)
            _fakes.Log.Of(nameof(IPlayerPresenceGrain.WatchRoomAsync)).Single().Args[0]!;
        request
            .Should()
            .BeEquivalentTo(
                new RoomWatchRequest
                {
                    RoomId = RoomId.Parse(3),
                    Plugin = "portals",
                    Envelope = Envelope,
                    LeaseMs = 60_000,
                    ThroughLinkedPair = true,
                }
            );
        _fakes
            .Log.Of(nameof(IPlayerPresenceGrain.UnwatchRoomAsync))
            .Single()
            .Args.Take(2)
            .Should()
            .Equal(RoomId.Parse(3), "portals");
        _leases.RoomsOf("portals").Should().Be(0);
    }

    private static readonly PluginMessageComposer Envelope = new()
    {
        Name = "portals.room",
        Body = [0, 0, 0, 3],
    };

    private RoomHold Hold() =>
        (RoomHold)_fakes.Log.Of(nameof(IRoomGrain.HoldAsync)).Last().Args[0]!;

    private HostHotel Hotel() =>
        new(
            _fakes.Create<IHotelPlayers>(),
            new HostHotelRooms(
                _fakes.Create<IDbContextFactory<TurboDbContext>>(),
                _fakes.Create<IGrainFactory>(),
                _fakes.Create<INavigatorService>(),
                _leases,
                _fakes.Create<IRoomService>()
            ),
            _fakes.Create<IHotelBots>(),
            _fakes.Create<IHotelWallet>(),
            _fakes.Create<IHotelInventory>(),
            _fakes.Create<IHotelCatalog>(),
            _fakes.Create<IHotelNotify>(),
            _fakes.Create<IHotelNavigator>(),
            _fakes.Create<IHotelWired>(),
            _fakes.Create<IHotelStatus>(),
            _clock
        );

    private const string PORTALS = """
        using System;
        using System.Threading;
        using System.Threading.Tasks;
        using Microsoft.Extensions.DependencyInjection;
        using Turbo.Contracts.Hotel;
        using Turbo.Contracts.Plugins;
        using Turbo.Contracts.Plugins.Exports;
        using Turbo.Primitives.Rooms;

        public sealed class Portals(IHotel hotel)
        {
            public Task HoldLobbyAsync(CancellationToken ct) =>
                hotel.Rooms.HoldAsync(new RoomId(12), "lobby", TimeSpan.FromMinutes(1), ct);
        }

        public sealed class PortalsPlugin : ITurboPlugin
        {
            public string Key => "portals";
            public string Version => "1.0.0";
            public void ConfigureServices(IServiceCollection services, PluginManifest manifest) =>
                services.AddSingleton<Portals>();
            public Task StartAsync(IServiceProvider services, CancellationToken ct) =>
                services.GetRequiredService<Portals>().HoldLobbyAsync(ct);
            public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
            public Task BindExportsAsync(IExportBinder binder, IServiceProvider services) => Task.CompletedTask;
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
        """;
}
