using System.Collections.Immutable;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Context;
using Turbo.Database.Entities.Room;
using Turbo.Primitives.Action;
using Turbo.Primitives.Players;
using Turbo.Rooms.Grains.Modules;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// A room reads its rights from the database the first time it needs them, and writes them into
/// its own state when the read answers. That write belongs to the room's turn: a grain runs one
/// turn at a time on its own scheduler, and nothing else guards its state. The read was awaited
/// with <c>ConfigureAwait(false)</c>, which in Orleans leaves the grain's scheduler as well as any
/// synchronisation context, so the rights were written from the thread pool while another turn
/// (a room tick, the next request) held the room.
/// </summary>
public sealed class RoomRightsLoadTurnTests
{
    private const int OWNER = 1;
    private const int ROOM = 1;
    private static readonly PlayerId Holder = 5;

    private static readonly ActionContext Owner = ActionContext.CreateForPlayer(OWNER, ROOM);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Rights_read_from_the_database_are_written_in_the_rooms_own_turn()
    {
        var store = new InMemoryDb();
        await using (var db = await store.CreateDbContextAsync(Ct))
        {
            db.RoomRights.Add(
                new RoomRightEntity { RoomEntityId = ROOM, PlayerEntityId = Holder.Value }
            );
            await db.SaveChangesAsync(Ct);
        }

        var database = new AnsweredLaterDb(store);
        var room = new LiveRoomHarness();
        room.Module<RoomSecurityModule>().UseDatabase(database);
        room.Fakes.Handlers["GetPlayerNamesAsync"] = call =>
            Task.FromResult(
                ((List<PlayerId>)call.Args[0]!).ToImmutableDictionary(x => x, _ => "holder")
            );

        // The grain's scheduler: one turn at a time.
        var turns = new ConcurrentExclusiveSchedulerPair().ExclusiveScheduler;

        var listing = Task
            .Factory.StartNew(
                () => room.Room.GetControllersAsync(Owner, Ct),
                Ct,
                TaskCreationOptions.None,
                turns
            )
            .Unwrap();

        // The owner's request is waiting on the database; the room is free for its next turn.
        await database.Asked.WaitAsync(Ct);

        using var tickRunning = new ManualResetEventSlim();
        using var tickMayEnd = new ManualResetEventSlim();
        var tick = Task.Factory.StartNew(
            () =>
            {
                tickRunning.Set();
                tickMayEnd.Wait(Ct);
            },
            Ct,
            TaskCreationOptions.None,
            turns
        );
        tickRunning.Wait(Ct);

        // The database answers on a thread of its own while the tick holds the room.
        await Task.Run(database.Answer, Ct);
        var loadedDuringTick = room.State.IsRightsLoaded;
        var rightsDuringTick = room.State.PlayerIdsWithRights.Count;

        tickMayEnd.Set();
        await tick;
        var controllers = await listing.WaitAsync(Ct);

        loadedDuringTick
            .Should()
            .BeFalse("the rights are the room's state and only its own turn may write them");
        rightsDuringTick.Should().Be(0);
        controllers!.Value.Select(x => x.PlayerId).Should().Equal(Holder);
    }

    /// <summary>
    /// A database whose first context arrives when the test says, from whichever thread it says,
    /// as a real connection's does: the in-memory provider otherwise answers before the caller
    /// has even awaited, and no await ever yields.
    /// </summary>
    private sealed class AnsweredLaterDb(InMemoryDb inner) : IDbContextFactory<TurboDbContext>
    {
        private readonly TaskCompletionSource _asked = new(
            TaskCreationOptions.RunContinuationsAsynchronously
        );

        // Continuations run inline when it is answered, as an I/O completion runs them.
        private readonly TaskCompletionSource<TurboDbContext> _answer = new();

        private int _calls;

#pragma warning disable VSTHRD003 // Both are the test's own signals, completed by the test.
        public Task Asked => _asked.Task;

        public TurboDbContext CreateDbContext() => inner.CreateDbContext();

        public Task<TurboDbContext> CreateDbContextAsync(CancellationToken ct = default)
        {
            if (Interlocked.Increment(ref _calls) > 1)
                return inner.CreateDbContextAsync(ct);

            _asked.SetResult();

            return _answer.Task;
        }
#pragma warning restore VSTHRD003

        public void Answer() => _answer.SetResult(inner.CreateDbContext());
    }
}
