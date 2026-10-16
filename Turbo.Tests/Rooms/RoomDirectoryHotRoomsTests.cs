using System.Runtime.CompilerServices;
using FluentAssertions;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Grains;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Rooms.Grains;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// The room directory counts who entered each room within the hot-room window
/// (<c>RoomConfig.HotRoomWindowSeconds</c>, 600 s), which the navigator's hot rooms are ordered
/// by: each player once, and an entry stops counting when it leaves the window even though
/// nothing else in the directory changed.
/// </summary>
public sealed class RoomDirectoryHotRoomsTests
{
    private readonly ManualTimeProvider _time = new(
        new DateTimeOffset(2026, 10, 16, 0, 0, 0, TimeSpan.Zero)
    );
    private readonly IRoomDirectoryGrain _directory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public RoomDirectoryHotRoomsTests()
    {
        _directory = (IRoomDirectoryGrain)
            GrainHarness.Create(
                typeof(RoomGrain).Assembly,
                "Turbo.Rooms.Grains.RoomDirectoryGrain",
                new Fakes()
            );
        RoomHarness.SetMember(_directory, "_time", (TimeProvider)_time);
    }

    [Fact]
    public async Task Recent_visitors_count_each_player_once_within_the_window()
    {
        await RoomAsync(1);
        await RoomAsync(2);

        await _directory.AddPlayerToRoomAsync(10, 1, Ct);
        await _directory.AddPlayerToRoomAsync(11, 1, Ct);
        await _directory.RemovePlayerFromRoomAsync(10, 1, Ct);
        await _directory.AddPlayerToRoomAsync(10, 1, Ct); // back in: still one visitor
        await _directory.AddPlayerToRoomAsync(12, 2, Ct);

        (await VisitorsAsync()).Should().Equal((1, 2), (2, 1));

        _time.Advance(TimeSpan.FromSeconds(599));
        await _directory.AddPlayerToRoomAsync(13, 2, Ct);
        _time.Advance(TimeSpan.FromSeconds(2));

        // The first entries left the window; only player 13's is still counted.
        (await VisitorsAsync())
            .Should()
            .Equal((1, 0), (2, 1));
    }

    [Fact]
    public async Task A_room_that_deactivates_forgets_its_visitors()
    {
        await RoomAsync(1);
        await _directory.AddPlayerToRoomAsync(10, 1, Ct);
        await _directory.RemovePlayerFromRoomAsync(10, 1, Ct);
        await _directory.RemoveActiveRoomAsync(1, listingChanged: false, Ct);
        await RoomAsync(1);

        (await VisitorsAsync()).Should().Equal((1, 0));
    }

    private async Task<(int RoomId, int Visitors)[]> VisitorsAsync()
    {
        var view = await _directory.GetListingViewAsync(Guid.Empty, 0, true, Ct);

        return
        [
            .. view
                .ActiveRooms.OrderBy(x => x.RoomId.Value)
                .Select(x => ((int)x.RoomId.Value, x.RecentVisitors)),
        ];
    }

    private async Task RoomAsync(int roomId)
    {
        var room = (RoomInfoSnapshot)
            RuntimeHelpers.GetUninitializedObject(typeof(RoomInfoSnapshot));
        RoomHarness.SetMember(room, "RoomId", (RoomId)roomId);
        RoomHarness.SetMember(room, "OwnerId", (PlayerId)1);

        await _directory.UpsertActiveRoomAsync(room, Ct);
    }
}
