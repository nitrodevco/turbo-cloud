using System.Reflection;
using FluentAssertions;
using Turbo.Primitives.Rooms.Events;
using Turbo.Primitives.Rooms.Grains;
using Turbo.Rooms.Grains;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// The room directory announces a player entering or leaving a room, naming both, for whatever
/// watches the hotel (the admin panel's live updates). Telling it what it already knows (an enter
/// for someone inside, a leave for someone not) is not a change and says nothing.
/// </summary>
public class RoomDirectoryActivityEventsTests
{
    private readonly TestEventBus _events = new();
    private readonly IRoomDirectoryGrain _directory;

    public RoomDirectoryActivityEventsTests()
    {
        var grain = GrainHarness.Create(
            typeof(RoomGrain).Assembly,
            "Turbo.Rooms.Grains.RoomDirectoryGrain",
            new Fakes()
        );

        grain
            .GetType()
            .GetField("_eventSystem", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(grain, _events.System);
        _events.Record<RoomActivityChangedEvent>();
        _directory = (IRoomDirectoryGrain)grain;
    }

    private async Task<(int Room, int? Player)[]> AnnouncedAsync()
    {
        await SessionHarness.Settle();

        return
        [
            .. _events
                .Of<RoomActivityChangedEvent>()
                .Select(x => (x.RoomId.Value, x.PlayerId?.Value)),
        ];
    }

    [Fact]
    public async Task EnteringAndLeaving_AreAnnounced_WithTheRoomAndThePlayer()
    {
        await _directory.AddPlayerToRoomAsync(7, 42, CancellationToken.None);
        await _directory.RemovePlayerFromRoomAsync(7, 42, CancellationToken.None);

        (await AnnouncedAsync()).Should().Equal((42, 7), (42, 7));
    }

    [Fact]
    public async Task WhatTheDirectoryAlreadyKnew_IsNotAnnounced()
    {
        await _directory.AddPlayerToRoomAsync(7, 42, CancellationToken.None);
        await _directory.AddPlayerToRoomAsync(7, 42, CancellationToken.None);
        await _directory.RemovePlayerFromRoomAsync(8, 42, CancellationToken.None);
        await _directory.RemovePlayerFromRoomAsync(7, 43, CancellationToken.None);

        (await AnnouncedAsync()).Should().Equal([(42, 7)], "only the first enter changed anything");
    }
}
