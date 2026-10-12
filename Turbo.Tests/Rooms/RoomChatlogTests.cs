using System.Reflection;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Turbo.Events;
using Turbo.Events.Registry;
using Turbo.Primitives.Action;
using Turbo.Primitives.Navigator.Enums;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Grains;
using Turbo.Primitives.Rooms.Snapshots.Chat;
using Turbo.Rooms.Configuration;
using Turbo.Rooms.Grains;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// The chat log is what moderators read to judge a report. A line was stamped when the room's
/// buffer reached the database, seconds after it was said (longer after a failed write), so it
/// could sort after the reply to it; and only the filtered line was kept, so a player who typed
/// abuse the filter caught looked to have said nothing worse than "bobba".
/// </summary>
public sealed class RoomChatlogTests : IDisposable
{
    private const int ROOM = 1;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly SqliteDb _db = new();

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task A_filtered_line_is_logged_with_what_was_typed_and_when_it_was_said()
    {
        var room = new WiredRoom(8, 8);
        var speaker = room.Enter(1, 0, 0);
        RoomHarness.SetField(
            room.Harness.Room,
            "_eventSystem",
            new EventSystem(
                new EventRegistry(room.Harness.Services, NullLogger<EventRegistry>.Instance)
            )
        );
        RoomHarness.SetField(
            room.Harness.Room,
            "_roomConfig",
            new RoomConfig { ChatFloodWindowMs = 0, ChatlogEnabled = true }
        );
        RoomHarness.SetField(room.Harness.Room, "_wordFilter", new PassThroughWordFilter());
        RoomHarness.SetMember(
            RoomHarness.GetMember(room.Harness.State, "RoomSnapshot")!,
            "ChatProtection",
            ChatFloodSensitivityType.Normal
        );
        var filter = (HashSet<string>)RoomHarness.GetMember(room.Harness.State, "FilterWords")!;
        filter.Add("darn");
        RoomHarness.SetMember(room.Harness.State, "IsFilterLoaded", true);

        foreach (var line in new[] { "oh darn it", "hello" })
            await room.Harness.Room.SendChatFromPlayerAsync(
                ActionContext.CreateForPlayer(speaker.PlayerId, ROOM),
                RoomChatType.Chat,
                line,
                0,
                Ct
            );
        await HandOverAsync(room);

        room.Harness.Fakes.Log.Of(nameof(IRoomPersistenceGrain.EnqueueChatlogsAsync))
            .Should()
            .ContainSingle()
            .Which.Args[0]
            .Should()
            .BeAssignableTo<List<RoomChatlogSnapshot>>()
            .Which.Should()
            .SatisfyRespectively(
                first =>
                {
                    (first.Text, first.Typed).Should().Be(("oh bobba it", "oh darn it"));
                    first.SaidAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
                },
                second => (second.Text, second.Typed).Should().Be(("hello", (string?)null))
            );
    }

    [Fact]
    public async Task A_line_is_written_with_the_moment_it_was_said_not_when_it_was_flushed()
    {
        var said = DateTime.UtcNow.AddMinutes(-3);
        var grain = GrainHarness.Create(
            typeof(RoomGrain).Assembly,
            "Turbo.Rooms.Grains.RoomPersistenceGrain",
            new Fakes(),
            _db
        );
        RoomHarness.SetMember(RoomHarness.GetField(grain, "_state")!, "RoomId", RoomId.Parse(ROOM));

        await ((IRoomPersistenceGrain)grain).EnqueueChatlogsAsync(
            [
                new RoomChatlogSnapshot
                {
                    RoomId = ROOM,
                    PlayerId = PlayerId.Parse(7),
                    Text = "oh bobba it",
                    Typed = "oh darn it",
                    SaidAt = said,
                },
            ],
            Ct
        );
        await (Task)
            grain
                .GetType()
                .GetMethod("FlushChatlogsAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(grain, [Ct])!;

        await using var db = await _db.CreateDbContextAsync(Ct);
        var row = await db.Chatlogs.SingleAsync(Ct);
        (row.Message, row.TypedMessage).Should().Be(("oh bobba it", "oh darn it"));
        row.CreatedAt.Should().BeCloseTo(said, TimeSpan.FromSeconds(1));
    }

    private static Task HandOverAsync(WiredRoom room) =>
        (Task)
            typeof(RoomGrain)
                .GetMethod(
                    "HandOverToPersistenceAsync",
                    BindingFlags.Instance | BindingFlags.NonPublic
                )!
                .Invoke(room.Harness.Room, [Ct])!;
}
