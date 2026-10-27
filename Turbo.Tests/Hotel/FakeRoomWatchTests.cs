using FluentAssertions;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Plugins.Messages;
using Turbo.Primitives.Rooms.Watching;
using Turbo.Testing;
using Xunit;

namespace Turbo.Tests.Hotel;

/// <summary>
/// The fake hotel keeps the running hotel's hold and watch rules (docs/plugin-room-watch.md), so a
/// plugin's tests of its portals see what the hotel would do: a door or a ban keeps a watcher out,
/// a player watches a few rooms, and every lease runs out by the hotel's clock.
/// </summary>
public sealed class FakeRoomWatchTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly TimeSpan MINUTE = TimeSpan.FromMinutes(1);

    [Fact]
    public async Task Holds_and_watches_keep_the_hotels_rules_and_run_out_by_its_clock()
    {
        var hotel = new FakeHotel();
        var alice = hotel.Players.Add("Alice", online: true);
        var bob = hotel.Players.Add("Bob");
        var lobby = hotel.Rooms.Add("Lobby", alice);
        var locked = hotel.Rooms.Add("Locked", alice);
        locked.DoorClosed = true;
        var envelope = new Portal(lobby.Id.Value);

        (await hotel.Rooms.HoldAsync(lobby, "sign", MINUTE, Ct)).Should().Be(RoomHoldOutcome.Held);
        (await hotel.Rooms.WatchAsync(alice, lobby, envelope, TimeSpan.FromMinutes(2), Ct))
            .Should()
            .Be(RoomWatchOutcome.Watching);
        (await hotel.Rooms.WatchAsync(alice, locked, envelope, MINUTE, Ct))
            .Should()
            .Be(RoomWatchOutcome.DoorClosed);
        (await hotel.Rooms.WatchAsync(bob, lobby, envelope, MINUTE, Ct))
            .Should()
            .Be(RoomWatchOutcome.NotOnline);

        hotel.Rooms.Watching(alice).Should().Equal(lobby.Id);
        hotel.Rooms.WatchEnvelope(alice, lobby).Should().Be(envelope);

        hotel.Clock.Advance(TimeSpan.FromSeconds(90));
        hotel.Rooms.IsHeld(lobby).Should().BeTrue("the watch's lease still runs");

        hotel.Clock.Advance(TimeSpan.FromSeconds(31));
        hotel.Rooms.IsHeld(lobby).Should().BeFalse();
        hotel.Rooms.Watching(alice).Should().BeEmpty();
    }

    [PluginMessage("portals.room")]
    public sealed record Portal(int RoomId, IComposer? Carried = null) : IPluginMessage;
}
