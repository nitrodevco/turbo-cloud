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
        var grain = GrainHarness.Create<RoomDirectoryGrain>(new Fakes());

        GrainHarness.Replace(grain, _events.System);
        _events.Record<RoomActivityChangedEvent>();
        _directory = (IRoomDirectoryGrain)grain;
    }

    private async Task<(int Room, int? Player)[]> AnnouncedAsync(int count)
    {
        await _events.WhenPublishedAsync<RoomActivityChangedEvent>(count);

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

        (await AnnouncedAsync(2)).Should().Equal((42, 7), (42, 7));
    }

    [Fact]
    public async Task WhatTheDirectoryAlreadyKnew_IsNotAnnounced()
    {
        await _directory.AddPlayerToRoomAsync(7, 42, CancellationToken.None);
        await _directory.AddPlayerToRoomAsync(7, 42, CancellationToken.None);
        await _directory.RemovePlayerFromRoomAsync(8, 42, CancellationToken.None);
        await _directory.RemovePlayerFromRoomAsync(7, 43, CancellationToken.None);

        (await AnnouncedAsync(1))
            .Should()
            .Equal([(42, 7)], "only the first enter changed anything");
    }
}
