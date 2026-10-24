using System.Reflection;
using FluentAssertions;
using Orleans.Runtime;
using Turbo.Rooms.Grains;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// An empty room unloads itself about 35 s after the last user left, as Habbo's does (official
/// client capture 2026-10-24, WIN63-202610201032, wired matrix trigger.3.R4: still loaded 5/5
/// after 12-34 s away, unloaded 4/5 after 36-40 s and 3/3 after 42-49 s), rather than at the
/// room directory's next check, up to five minutes later.
/// </summary>
public sealed class RoomEmptyUnloadTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // trigger.3.R4: never unloaded within 34 s, unloaded after 36.1 s.
    [Fact]
    public async Task An_empty_room_unloads_itself_after_35_seconds()
    {
        var room = new WiredRoom();
        var entered = room.Enter(1, 2, 2);

        await TickForAsync(room, 60_000);
        Deactivations(room).Should().Be(0, "someone is in the room");

        room.Harness.State.AvatarsByObjectId.Remove(entered.ObjectId);
        room.Harness.State.AvatarsByPlayerId.Clear();

        await TickForAsync(room, 34_000);
        Deactivations(room).Should().Be(0, "34 s after the last user left it is still loaded");

        await TickForAsync(room, 2_000);
        Deactivations(room).Should().Be(1, "36 s after the last user left it unloads, once");
    }

    private static int Deactivations(WiredRoom room) =>
        room.Harness.Fakes.Log.Calls.Count(x =>
            x.Interface == typeof(IGrainRuntime)
            && x.Method == nameof(IGrainRuntime.DeactivateOnIdle)
        );

    // The room's own tick, every avatar tick (an empty room is dormant and ticks at that pace).
    private static async Task TickForAsync(WiredRoom room, int forMs)
    {
        var tick = typeof(RoomGrain).GetMethod(
            "ProcessRoomTickAsync",
            BindingFlags.Instance | BindingFlags.NonPublic
        )!;

        for (var elapsed = 0; elapsed < forMs; elapsed += 500)
        {
            room.Harness.Clock.AdvanceMs(500);
            await (Task)tick.Invoke(room.Harness.Room, [Ct])!;
        }
    }
}
