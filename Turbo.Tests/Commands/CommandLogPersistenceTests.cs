using System.Reflection;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Context;
using Turbo.Database.Entities.Room;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Commands.Snapshots;
using Turbo.Primitives.Rooms.Grains;
using Turbo.Rooms.Configuration;
using Turbo.Rooms.Grains;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Commands;

public class CommandLogPersistenceTests
{
    private const string GRAIN = "Turbo.Rooms.Grains.RoomPersistenceGrain";

    private static CommandLogSnapshot Use(string arguments, int player = 2) =>
        new()
        {
            RoomId = 7,
            PlayerId = player,
            Command = "kick",
            Arguments = arguments,
            Outcome = CommandOutcome.Completed,
            LoggedAtUtc = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc),
        };

    private static object NewGrain(IDbContextFactory<TurboDbContext> db) =>
        GrainHarness.Create(typeof(RoomGrain).Assembly, GRAIN, new Fakes(), db);

    private static Task<bool> Flush(object grain) =>
        (Task<bool>)
            grain
                .GetType()
                .GetMethod("FlushCommandLogsAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(grain, [CancellationToken.None])!;

    private static Queue<CommandLogSnapshot> Queue(object grain) =>
        (Queue<CommandLogSnapshot>)
            RoomHarness.GetMember(RoomHarness.GetField(grain, "_state")!, "PendingCommandLogs")!;

    [Fact]
    public async Task AFlush_WritesTheQueuedUses_WithWhenTheyRan()
    {
        var db = new InMemoryDb();
        var grain = NewGrain(db);

        await ((IRoomPersistenceGrain)grain).EnqueueCommandLogsAsync(
            [Use("alice"), Use("bob", player: 3)],
            CancellationToken.None
        );

        (await Flush(grain)).Should().BeTrue();

        await using var ctx = db.CreateDbContext();
        var rows = await ctx.CommandLogs.OrderBy(x => x.Arguments).ToListAsync();
        rows.Select(x => (x.RoomEntityId, x.PlayerEntityId, x.Command, x.Arguments, x.Outcome))
            .Should()
            .Equal((7, 2, "kick", "alice", "completed"), (7, 3, "kick", "bob", "completed"));
        rows.Should().OnlyContain(x => x.CreatedAt.Year == 2026 && x.CreatedAt.Month == 10);
        Queue(grain).Should().BeEmpty();
    }

    [Fact]
    public async Task ArgumentsLongerThanTheColumn_AreCutRatherThanFailingTheBatch()
    {
        var db = new InMemoryDb();
        var grain = NewGrain(db);

        await ((IRoomPersistenceGrain)grain).EnqueueCommandLogsAsync(
            [Use(new string('x', 1000))],
            CancellationToken.None
        );

        (await Flush(grain)).Should().BeTrue();

        await using var ctx = db.CreateDbContext();
        (await ctx.CommandLogs.SingleAsync())
            .Arguments.Should()
            .HaveLength(CommandLogEntity.ARGUMENTS_MAX_LENGTH);
    }

    [Fact]
    public async Task AFailedWrite_KeepsEveryUseQueued_InOrder_AndReportsNoProgress()
    {
        var grain = NewGrain(new BrokenDb());

        await ((IRoomPersistenceGrain)grain).EnqueueCommandLogsAsync(
            [Use("first"), Use("second")],
            CancellationToken.None
        );
        await ((IRoomPersistenceGrain)grain).EnqueueCommandLogsAsync(
            [Use("third")],
            CancellationToken.None
        );

        (await Flush(grain)).Should().BeFalse();

        Queue(grain).Select(x => x.Arguments).Should().Equal("first", "second", "third");
    }

    [Fact]
    public async Task TheBuffer_IsBounded_AndDropsTheOldestFirst()
    {
        var grain = NewGrain(new InMemoryDb());
        var cap = new RoomConfig().MaxPendingCommandLogs;

        await ((IRoomPersistenceGrain)grain).EnqueueCommandLogsAsync(
            [.. Enumerable.Range(0, cap + 10).Select(i => Use(i.ToString()))],
            CancellationToken.None
        );

        Queue(grain).Should().HaveCount(cap);
        Queue(grain).First().Arguments.Should().Be("10");
    }

    private sealed class BrokenDb : IDbContextFactory<TurboDbContext>
    {
        public TurboDbContext CreateDbContext() => throw new InvalidOperationException("db down");

        public Task<TurboDbContext> CreateDbContextAsync(CancellationToken ct = default) =>
            throw new InvalidOperationException("db down");
    }
}
