using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Context;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Grains;
using Turbo.Primitives.Rooms.Snapshots.Wired;
using Turbo.Rooms.Grains;
using Turbo.Rooms.Grains.Systems;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// A room's wired log survives its unload (official client capture 2026-10-25T01-46-55 ..
/// 01-48-44, wired matrix action.49.R6, 1/1): with both accounts on the hotel view the room
/// unloaded, and after it loaded again "View full logs" still listed all 123 lines, the
/// 01:37 "W8L-click" / "W8NEG-click" and lines from 2026-10-14 among them. Ours kept the log in
/// the room grain only, so an unload lost it.
/// </summary>
public sealed class WiredRoomLogPersistenceTests
{
    private const string GRAIN = "Turbo.Rooms.Grains.RoomPersistenceGrain";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static IRoomPersistenceGrain NewPersistence(IDbContextFactory<TurboDbContext> db) =>
        (IRoomPersistenceGrain)
            GrainHarness.Create(typeof(RoomGrain).Assembly, GRAIN, new Fakes(), db);

    private static WiredRoomLogEntrySnapshot Line(string message, long at) =>
        new()
        {
            Id = 1,
            Level = 0,
            Source = RoomWiredSystem.LOG_SOURCE_WIRED,
            Message = message,
            TimestampMs = at,
            TimestampText = string.Empty,
        };

    [Fact]
    public async Task A_room_loaded_again_lists_the_wired_log_it_wrote_before_the_unload()
    {
        using var db = new SqliteDb();
        // 2026-10-14 and 2026-10-25, as old as the lines the official room still listed.
        const long OLD = 1_791_936_000_000;
        const long NOW = 1_792_892_255_657;

        var before = NewPersistence(db);
        await before.EnqueueWiredLogsAsync(
            [Line("W8L-old", OLD), Line("W8L-click", NOW), Line("W8NEG-click", NOW + 3_000)],
            200,
            Ct
        );

        // The room unloads and loads again: what was still queued is written before it is read.
        var stored = await before.LoadWiredLogsAsync(200, Ct);
        var reloaded = await NewPersistence(db).LoadWiredLogsAsync(200, Ct);

        stored.Select(x => x.Message).Should().Equal("W8L-old", "W8L-click", "W8NEG-click");
        reloaded
            .Select(x => (x.Message, x.TimestampMs))
            .Should()
            .Equal(("W8L-old", OLD), ("W8L-click", NOW), ("W8NEG-click", NOW + 3_000));

        var room = new WiredRoom(4, 4);
        var wired = room.Harness.Module<RoomWiredSystem>();

        wired.LoadRoomLogs(reloaded);
        wired.RecordLog(WiredLogLevelType.Info, "W8NEG-after");

        var page = wired.GetRoomLogPage(1, 20, -1, -1, string.Empty);

        page.TotalEntries.Should().Be(4);
        page.Entries.Select(x => x.Message)
            .Should()
            .Equal("W8NEG-after", "W8NEG-click", "W8L-click", "W8L-old");
        page.Entries.Select(x => x.Id).Should().OnlyHaveUniqueItems();
        page.Entries.Should()
            .OnlyContain(x =>
                System.Text.RegularExpressions.Regex.IsMatch(
                    x.TimestampText,
                    @"^\d\d-\d\d-\d{4} \d\d:\d\d:\d\d$"
                )
            );
    }

    [Fact]
    public async Task Only_the_newest_lines_the_room_keeps_are_stored()
    {
        using var db = new SqliteDb();
        var persistence = NewPersistence(db);

        await persistence.EnqueueWiredLogsAsync([Line("a", 1), Line("b", 2)], 2, Ct);
        await persistence.LoadWiredLogsAsync(2, Ct);
        await persistence.EnqueueWiredLogsAsync([Line("c", 3)], 2, Ct);
        await persistence.LoadWiredLogsAsync(2, Ct);

        await using var ctx = await db.CreateDbContextAsync(Ct);

        (await ctx.RoomWiredLogs.OrderBy(x => x.Id).Select(x => x.Message).ToListAsync(Ct))
            .Should()
            .Equal("b", "c");
    }
}
