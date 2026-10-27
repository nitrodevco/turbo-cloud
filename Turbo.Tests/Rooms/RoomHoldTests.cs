using System.Reflection;
using FluentAssertions;
using Orleans.Runtime;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Players.Grains;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Events.RoomItem;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Watching;
using Turbo.Rooms.Grains;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Logic.Furniture.Floor;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// A plugin's hold keeps a room no one is in loaded and awake (a plugin drawing it beside another
/// room needs it to move), until the hold is released or its lease runs out; then the room goes
/// dormant and unloads 35 s later, as an empty room does.
/// </summary>
public sealed class RoomHoldTests
{
    private const int ROLLER = 41;
    private const int CRATE = 45;

    private readonly WiredRoom _room = new(8, 8);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private RoomGrain Room => _room.Harness.Room;

    public RoomHoldTests()
    {
        // Two rollers facing each other with a crate on them: it slides on every roller beat
        // while the room is awake.
        AddRoller(ROLLER, 1, 1, Rotation.East);
        AddRoller(ROLLER + 1, 2, 1, Rotation.West);
        _room.AddFloorItem(CRATE, 1, 1).SetPositionZ(Altitude.FromInt(100));
        _room.Harness.State.IsFurniLoaded = true;
    }

    [Fact]
    public async Task An_empty_room_is_dormant_its_rollers_still()
    {
        await RescanAsync();

        (await SlidesOverAsync(10_000)).Should().Be(0, "no one is in it and no one holds it");
    }

    [Fact]
    public async Task A_held_empty_room_stays_awake_and_loaded_until_the_lease_runs_out()
    {
        await RescanAsync();

        (await Room.HoldAsync(Hold("portal", leaseMs: 60_000), Ct))
            .Should()
            .Be(RoomHoldOutcome.Held);

        (await SlidesOverAsync(10_000)).Should().BeGreaterThan(0, "the hold keeps it awake");

        Room.DeactivateRoom();
        await SlidesOverAsync(45_000);
        Deactivations().Should().Be(0, "55 s in, the 60 s hold still runs");

        await SlidesOverAsync(5_000);
        (await SlidesOverAsync(10_000)).Should().Be(0, "the lease ran out: dormant again");
        await SlidesOverAsync(26_000);
        Deactivations().Should().Be(1, "unloaded 35 s after the last hold ended");
    }

    [Fact]
    public async Task A_released_hold_lets_the_room_sleep_and_unload_while_another_plugins_hold_keeps_it()
    {
        await RescanAsync();
        await Room.HoldAsync(Hold("portal", leaseMs: 600_000), Ct);
        await Room.HoldAsync(Hold("radio", leaseMs: 600_000), Ct);

        (await Room.ReleaseHoldAsync("portal", "a", default, Ct)).Should().BeTrue();
        (await SlidesOverAsync(40_000)).Should().BeGreaterThan(0, "the radio's hold still runs");
        Deactivations().Should().Be(0);

        await Room.ReleasePluginHoldsAsync("radio", Ct);
        (await SlidesOverAsync(10_000)).Should().Be(0);
        await SlidesOverAsync(30_000);
        Deactivations().Should().Be(1);
    }

    [Fact]
    public async Task A_plugins_release_ends_its_watchers_watches_too()
    {
        await Room.HoldAsync(Hold("portal", leaseMs: 60_000) with { Holder = "", Watcher = 7 }, Ct);

        await Room.ReleasePluginHoldsAsync("portal", Ct);

        _room
            .Harness.Fakes.Log.Of(nameof(IPlayerPresenceGrain.UnwatchRoomAsync))
            .Should()
            .ContainSingle()
            .Which.Args.Take(2)
            .Should()
            .Equal((RoomId)1, "portal");
    }

    [Fact]
    public async Task A_room_takes_a_limited_number_of_holds_and_a_renewal_is_not_one_more()
    {
        for (var i = 0; i < RoomWatchLimits.MAX_HOLDS_PER_ROOM; i++)
            (await Room.HoldAsync(Hold("portal", 60_000) with { Holder = $"h{i}" }, Ct))
                .Should()
                .Be(RoomHoldOutcome.Held);

        (await Room.HoldAsync(Hold("portal", 60_000) with { Holder = "one more" }, Ct))
            .Should()
            .Be(RoomHoldOutcome.RoomFull);
        (await Room.HoldAsync(Hold("portal", 60_000) with { Holder = "h0" }, Ct))
            .Should()
            .Be(RoomHoldOutcome.Held, "the same holder renews");
    }

    private static RoomHold Hold(string plugin, long leaseMs) =>
        new()
        {
            Plugin = plugin,
            Holder = "a",
            LeaseMs = leaseMs,
        };

    private int Deactivations() =>
        _room.Harness.Fakes.Log.Calls.Count(x =>
            x.Interface == typeof(IGrainRuntime)
            && x.Method == nameof(IGrainRuntime.DeactivateOnIdle)
        );

    // The room's own tick every 500 ms; how many roller slides it published meanwhile.
    private async Task<int> SlidesOverAsync(int forMs)
    {
        var tick = typeof(RoomGrain).GetMethod(
            "ProcessRoomTickAsync",
            BindingFlags.Instance | BindingFlags.NonPublic
        )!;
        var before = Slides();

        for (var elapsed = 0; elapsed < forMs; elapsed += 500)
        {
            _room.Harness.Clock.AdvanceMs(500);
            await (Task)tick.Invoke(Room, [Ct])!;
        }

        return Slides() - before;
    }

    private int Slides() =>
        _room
            .Harness.Fakes.RoomPublishes()
            .SelectMany(x => x.Composers)
            .Count(x => x is SlideObjectBundleMessageComposer);

    private void AddRoller(int id, int x, int y, Rotation rotation) =>
        _room
            .AddFloorItem(
                id,
                x,
                y,
                "roller",
                createLogic: (factory, ctx) => new FurnitureRollerLogic(factory, ctx)
            )
            .SetRotation(rotation);

    private Task RescanAsync() =>
        _room
            .Harness.Module<RoomRollerSystem>()
            .OnRoomEventAsync(
                new RoomRollerChangedEvent
                {
                    RoomId = 1,
                    CausedBy = ActionContext.CreateForSystem(1),
                    ObjectId = ROLLER,
                },
                Ct
            );
}
