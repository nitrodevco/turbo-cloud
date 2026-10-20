using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Orleans;
using Orleans.Runtime;
using Turbo.Database.Configuration;
using Turbo.Database.Context;
using Turbo.Database.Entities.Movements;
using Turbo.Database.Extensions;
using Turbo.Operations;
using Turbo.Operations.Movements;
using Turbo.Primitives.Movements;
using Turbo.Primitives.Movements.Enums;
using Turbo.Primitives.Movements.Grains;
using Turbo.Primitives.Players.Wallet;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Operations;

/// <summary>
/// Handing the asset movement outbox to plugin consumers (docs/movements.md "Consuming
/// movements"): settled rows in id order from each consumer's own cursor, the same rows again
/// after a consumer throws, a consumer registered late starting from its cursor, pruning that
/// reports the ids a consumer lost, rows that commit late delivered below the cursor, nothing
/// read while no consumer is registered, and two dispatchers at once applying each movement once.
/// </summary>
public sealed class AssetMovementDispatcherTests : IDisposable
{
    private static readonly DateTime Start = new(2026, 10, 18, 12, 0, 0, DateTimeKind.Utc);
    private static readonly CancellationToken Ct = CancellationToken.None;

    private readonly SqliteDb _db = new();
    private readonly ManualTimeProvider _clock = new(new DateTimeOffset(Start));
    private readonly Fakes _fakes = new();
    private readonly CapturingLogger<AssetMovementDispatcher> _logger = new();
    private readonly AssetMovementConsumerRegistry _registry;

    public AssetMovementDispatcherTests()
    {
        _registry = new AssetMovementConsumerRegistry(
            _fakes.Create<IGrainFactory>(),
            NullLogger<AssetMovementConsumerRegistry>.Instance
        );
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task SettledRows_AreHandedOverInIdOrder_AndAFreshRowWaitsForTheSettleWindow()
    {
        Row(1, ago: TimeSpan.FromMinutes(1));
        Row(2, ago: TimeSpan.FromSeconds(3));
        Row(3, ago: TimeSpan.FromMilliseconds(500));
        // Settled, but after an unsettled id: it waits with it.
        Row(4, ago: TimeSpan.FromMinutes(2));
        var consumer = new LedgerConsumer("ledger");
        using var registration = _registry.Register(consumer);
        var dispatcher = Dispatcher();

        (await dispatcher.DispatchAsync(Ct)).Should().BeTrue();

        consumer.Batches.Should().ContainSingle();
        consumer.Batches[0].Movements.Select(x => x.Id).Should().Equal(1, 2);
        consumer.Batches[0].Cursor.Should().Be(2);
        consumer.Cursor.Should().Be(2);

        _clock.Advance(TimeSpan.FromSeconds(2));
        await dispatcher.DispatchAsync(Ct);

        consumer.Batches.Should().HaveCount(2);
        consumer.Batches[1].Movements.Select(x => x.Id).Should().Equal(3, 4);
        consumer.Applied.Should().HaveCount(4);
    }

    [Fact]
    public async Task AFullBatch_IsFollowedByTheNextInTheSamePoll()
    {
        for (var id = 1; id <= 5; id++)
            Row(id);
        var consumer = new LedgerConsumer("ledger");
        using var registration = _registry.Register(consumer);

        await Dispatcher(batchSize: 2).DispatchAsync(Ct);

        consumer.Batches.Select(x => x.Movements.Length).Should().Equal(2, 2, 1);
        consumer.Cursor.Should().Be(5);
    }

    [Fact]
    public async Task AConsumerThatThrows_GetsTheSameRowsAgain_AndTheOthersAreNotHeldUp()
    {
        Row(1);
        Row(2);
        var failing = new LedgerConsumer("a-ledger") { FailuresLeft = 1 };
        var other = new LedgerConsumer("b-audit");
        using var r1 = _registry.Register(failing);
        using var r2 = _registry.Register(other);
        var dispatcher = Dispatcher();

        var act = () => dispatcher.DispatchAsync(Ct);

        await act.Should().NotThrowAsync();
        failing.Cursor.Should().Be(0);
        failing.Applied.Should().BeEmpty();
        other.Cursor.Should().Be(2);
        _logger
            .AtLeast(LogLevel.Error)
            .Should()
            .ContainSingle()
            .Which.Message.Should()
            .Contain("a-ledger");

        await dispatcher.DispatchAsync(Ct);

        failing.Batches.Select(x => x.Movements.Select(m => m.Id)).Should().HaveCount(2);
        failing.Batches[1].Movements.Select(x => x.Id).Should().Equal(1, 2);
        failing.Applied.Should().HaveCount(2);
        failing.Cursor.Should().Be(2);
        // The cursor is asked again after a failure, from what the consumer stored.
        failing.CursorReads.Should().Be(2);
        other.Batches.Should().ContainSingle();
    }

    [Fact]
    public async Task AConsumerRegisteredLate_StartsFromItsOwnCursor_AndWakesTheDispatcher()
    {
        Row(1);
        Row(2);
        Row(3);
        var first = new LedgerConsumer("ledger");
        using var r1 = _registry.Register(first);
        var dispatcher = Dispatcher();
        await dispatcher.DispatchAsync(Ct);
        var wakes = Wakes();

        var late = new LedgerConsumer("audit") { Cursor = 1 };
        using var r2 = _registry.Register(late);
        await dispatcher.DispatchAsync(Ct);

        Wakes().Should().Be(wakes + 1);
        late.Batches.Should().ContainSingle();
        late.Batches[0].Movements.Select(x => x.Id).Should().Equal(2, 3);
        late.Cursor.Should().Be(3);
        first.Batches.Should().ContainSingle("it has nothing new");
    }

    [Fact]
    public async Task AConsumerRegisteredAgain_AfterItsPluginReloaded_ResumesFromWhatItStored()
    {
        Row(1);
        var consumer = new LedgerConsumer("ledger");
        var registration = _registry.Register(consumer);
        var dispatcher = Dispatcher();
        await dispatcher.DispatchAsync(Ct);

        registration.Dispose();
        Row(2);
        await dispatcher.DispatchAsync(Ct);
        consumer.Batches.Should().ContainSingle("an unregistered consumer is not called");

        // The reloaded plugin's consumer, same name, its cursor read back from its own table.
        var reloaded = new LedgerConsumer("ledger") { Cursor = consumer.Cursor };
        using var again = _registry.Register(reloaded);
        await dispatcher.DispatchAsync(Ct);

        reloaded.Batches.Should().ContainSingle();
        reloaded.Batches[0].Movements.Select(x => x.Id).Should().Equal(2);
    }

    [Fact]
    public async Task PruningPastAConsumersCursor_ReportsTheIdsItLost_AndLeavesOneMarker()
    {
        for (var id = 1; id <= 5; id++)
            Row(id, ago: TimeSpan.FromHours(80));
        Row(6, ago: TimeSpan.FromHours(70));
        Row(7, ago: TimeSpan.FromMinutes(1));
        var behind = new LedgerConsumer("ledger") { Cursor = 2 };
        var ahead = new LedgerConsumer("audit") { Cursor = 6 };
        using var r1 = _registry.Register(behind);
        using var r2 = _registry.Register(ahead);
        var dispatcher = Dispatcher();

        await dispatcher.DispatchAsync(Ct);

        Outbox()
            .Select(x => (x.Id, x.Cause))
            .Should()
            .Equal(
                (5, MovementCauses.OUTBOX_PRUNED),
                (6, MovementCauses.ROOM_PICKUP),
                (7, MovementCauses.ROOM_PICKUP)
            );
        behind.Batches.Should().ContainSingle();
        behind.Batches[0].Gap.Should().Be(new OutboxGap(3, 5));
        behind.Batches[0].Movements.Select(x => x.Id).Should().Equal(6, 7);
        behind.Cursor.Should().Be(7);
        ahead.Batches.Single().Gap.Should().BeNull("it had read past what was pruned");
        ahead.Batches.Single().Movements.Select(x => x.Id).Should().Equal(7);

        // The next prune, once the hour is up, deletes the old marker and leaves a new one.
        _clock.Advance(TimeSpan.FromHours(10));
        var newcomer = new LedgerConsumer("newcomer");
        using var r3 = _registry.Register(newcomer);
        await dispatcher.DispatchAsync(Ct);

        Outbox()
            .Select(x => (x.Id, x.Cause))
            .Should()
            .Equal((6, MovementCauses.OUTBOX_PRUNED), (7, MovementCauses.ROOM_PICKUP));
        newcomer.Batches.Single().Gap.Should().Be(new OutboxGap(1, 6));
        newcomer.Batches.Single().Movements.Select(x => x.Id).Should().Equal(7);
    }

    // The server's currency ledger is where currency history is kept for good: rows it has not
    // read yet used to be pruned like anyone's, and the history lost them without a trace.
    [Fact]
    public async Task TheServersCurrencyLedger_HoldsPruningBackToWhatItHasRead()
    {
        for (var id = 1; id <= 5; id++)
            Row(id, ago: TimeSpan.FromHours(80));
        Row(6, ago: TimeSpan.FromMinutes(1));
        using (var db = _db.CreateDbContext())
        {
            db.MovementConsumerCursors.Add(
                new MovementConsumerCursorEntity { Name = CurrencyLedger.NAME, Cursor = 2 }
            );
            db.SaveChanges();
        }
        var plugin = new LedgerConsumer("plugin-audit");
        using var r1 = _registry.Register(new CurrencyLedger(_db, _clock));
        using var r2 = _registry.Register(plugin);

        await Dispatcher().DispatchAsync(Ct);

        Outbox()
            .Select(x => (x.Id, x.Cause))
            .Should()
            .Equal(
                (2, MovementCauses.OUTBOX_PRUNED),
                (3, MovementCauses.ROOM_PICKUP),
                (4, MovementCauses.ROOM_PICKUP),
                (5, MovementCauses.ROOM_PICKUP),
                (6, MovementCauses.ROOM_PICKUP)
            );
        plugin.Batches.Single().Gap.Should().Be(new OutboxGap(1, 2));

        // Read now, the rest of the old rows go on the next prune.
        _clock.Advance(TimeSpan.FromHours(1));
        await Dispatcher().DispatchAsync(Ct);

        Outbox()
            .Select(x => (x.Id, x.Cause))
            .Should()
            .Equal((5, MovementCauses.OUTBOX_PRUNED), (6, MovementCauses.ROOM_PICKUP));
    }

    [Fact]
    public async Task ARowThatCommitsAfterLaterRows_IsHandedOverLate_BelowTheCursor_Once()
    {
        Row(1);
        Row(3);
        var consumer = new LedgerConsumer("ledger");
        using var registration = _registry.Register(consumer);
        var dispatcher = Dispatcher();
        await dispatcher.DispatchAsync(Ct);
        consumer.Cursor.Should().Be(3);

        // Id 2 was taken by a save that committed only now.
        Row(2, op: "slow-save");
        await dispatcher.DispatchAsync(Ct);
        await dispatcher.DispatchAsync(Ct);

        consumer.Batches.Should().HaveCount(2);
        var late = consumer.Batches[1];
        late.IsLate.Should().BeTrue();
        late.Movements.Select(x => x.Id).Should().Equal(2);
        late.Cursor.Should().Be(3);
        consumer.Cursor.Should().Be(3);
        consumer.Applied.Should().Contain(("slow-save", 0));
    }

    [Fact]
    public async Task ASkippedId_IsNoLongerLookedFor_AfterTheReReadWindow()
    {
        Row(1);
        Row(3);
        var consumer = new LedgerConsumer("ledger");
        using var registration = _registry.Register(consumer);
        var dispatcher = Dispatcher();
        await dispatcher.DispatchAsync(Ct);

        _clock.Advance(TimeSpan.FromMinutes(6));
        Row(2);
        await dispatcher.DispatchAsync(Ct);

        consumer.Batches.Should().ContainSingle();
    }

    [Fact]
    public async Task NoConsumer_ReadsNothing_AndTheGrainStopsPolling()
    {
        Row(1, ago: TimeSpan.FromHours(100));
        var counting = new CountingDb(_db);
        var dispatcher = Dispatcher(db: counting);

        (await dispatcher.DispatchAsync(Ct)).Should().BeFalse();

        var registration = _registry.Register(new LedgerConsumer("ledger"));
        registration.Dispose();
        (await dispatcher.DispatchAsync(Ct)).Should().BeFalse();
        counting.Contexts.Should().Be(0, "no query, not even the prune");

        var grain = Grain(dispatcher);
        await grain.WakeAsync(Ct);
        var poll = PollTimer();
        await poll.Callback(poll.State, Ct);

        counting.Contexts.Should().Be(0);
        _fakes
            .Log.Calls.Should()
            .Contain(c => c.Interface == typeof(IGrainTimer) && c.Method == "Dispose");
        Pins().Last().Should().Be(TimeSpan.Zero, "handed back to idle collection");
    }

    [Fact]
    public async Task TheGrain_PollsWhileAConsumerIsRegistered_AndPinsItselfForTwoPeriods()
    {
        Row(1);
        var consumer = new LedgerConsumer("ledger");
        using var registration = _registry.Register(consumer);
        var grain = Grain(Dispatcher());

        await grain.WakeAsync(Ct);
        await grain.WakeAsync(Ct);
        var poll = PollTimer();
        await poll.Callback(poll.State, Ct);

        _fakes.Log.Of("RegisterGrainTimer").Should().ContainSingle("a second wake is a no-op");
        consumer.Cursor.Should().Be(1);
        Pins().Should().OnlyContain(x => x == TimeSpan.FromMilliseconds(2000));
    }

    [Fact]
    public async Task TwoActivationsAtOnce_DeliverRowsTwice_AndTheConsumerAppliesEachOnce()
    {
        for (var id = 1; id <= 4; id++)
            Row(id);
        var held = new TaskCompletionSource();
        var consumer = new LedgerConsumer("ledger") { Hold = held.Task };
        using var registration = _registry.Register(consumer);
        var a = Dispatcher();
        var b = Dispatcher();

        // A has read 1-4 and is still handing them over when B, which Orleans briefly runs
        // beside it, reads the consumer's cursor (still 0) and hands over 1-4, then 5-6.
        var aPoll = a.DispatchAsync(Ct);
        consumer.Hold = null;
        await b.DispatchAsync(Ct);
        Row(5);
        Row(6);
        await b.DispatchAsync(Ct);
        held.SetResult();
        await aPoll;
        await a.DispatchAsync(Ct);

        consumer.Delivered.Should().Be(12, "each activation handed over every row");
        consumer.Applied.Should().HaveCount(6);
        consumer.Cursor.Should().Be(6, "A's late batch does not move the stored cursor back");
    }

    [Fact]
    public async Task TheMovement_ComesOutAsItWasRecorded()
    {
        var pickup = new AssetMovement
        {
            OperationId = "op-pickup",
            Seq = 2,
            OccurredAtUtc = Start.AddMinutes(-5),
            Asset = new AssetRef(AssetKind.Furni, 41, 7, null),
            From = new AssetPlace(
                5,
                PlaceKind.Room,
                12,
                new RoomPosition(3, 4, 1.5, Rotation.East, null)
            ),
            To = new AssetPlace(6, PlaceKind.Inventory, 0, null),
            ActorPlayerId = 6,
            Cause = MovementCauses.ROOM_PICKUP,
            CauseRef = "room-12",
            Snapshot = new AssetSnapshot(7, "{\"state\":1}"),
        };
        var credits = new AssetMovement
        {
            OperationId = "op-buy",
            Seq = 0,
            OccurredAtUtc = Start.AddMinutes(-4),
            Asset = new AssetRef(AssetKind.Currency, 0, 0, CurrencyKind.ActivityPoints(5)),
            From = new AssetPlace(5, PlaceKind.Wallet, 0, null),
            To = AssetPlace.Nowhere,
            Amount = 25,
            ActorPlayerId = 5,
            Cause = MovementCauses.CATALOG_PURCHASE,
        };
        Insert(pickup.ToOutboxEntity(Start.AddMinutes(-1)));
        Insert(credits.ToOutboxEntity(Start.AddMinutes(-1)));
        var consumer = new LedgerConsumer("ledger");
        using var registration = _registry.Register(consumer);

        await Dispatcher().DispatchAsync(Ct);

        var delivered = consumer.Batches.Single().Movements;
        delivered[0].Movement.Should().Be(pickup);
        delivered[0].WrittenAtUtc.Should().Be(Start.AddMinutes(-1));
        delivered[1].Movement.Should().Be(credits);
    }

    [Fact]
    public void TheRegistry_RefusesASecondConsumerOfTheSameName_UntilTheFirstIsDisposed()
    {
        var first = _registry.Register(new LedgerConsumer("ledger"));

        var twice = () => _registry.Register(new LedgerConsumer("ledger"));
        var nameless = () => _registry.Register(new LedgerConsumer(" "));

        twice.Should().Throw<InvalidOperationException>();
        nameless.Should().Throw<ArgumentException>();
        var stopping = _registry.Current.Single().Stopping;

        first.Dispose();
        first.Dispose();

        stopping.IsCancellationRequested.Should().BeTrue();
        _registry.Current.Should().BeEmpty();
        using var again = _registry.Register(new LedgerConsumer("ledger"));
        _registry.Current.Should().ContainSingle();
    }

    private AssetMovementDispatcher Dispatcher(
        int batchSize = 500,
        IDbContextFactory<TurboDbContext>? db = null
    ) =>
        new(
            db ?? _db,
            Options.Create(new MovementsConfig { BatchSize = batchSize }),
            _registry,
            _clock,
            _logger
        );

    private IAssetMovementDispatcherGrain Grain(AssetMovementDispatcher dispatcher)
    {
        var grain = GrainHarness.Create(
            typeof(OperationsModule).Assembly,
            "Turbo.Operations.Grains.AssetMovementDispatcherGrain",
            _fakes,
            _db
        );
        RoomHarness.SetMember(grain, "_dispatcher", dispatcher);

        return (IAssetMovementDispatcherGrain)grain;
    }

    private sealed record Poll(Func<object?, CancellationToken, Task> Callback, object? State);

    private Poll PollTimer() =>
        _fakes
            .Log.Of("RegisterGrainTimer")
            .Select(call => new Poll(
                (Func<object?, CancellationToken, Task>)call.Args[1]!,
                call.Args[2]
            ))
            .Single();

    private List<TimeSpan> Pins() =>
        [.. _fakes.Log.Of("DelayDeactivation").Select(call => (TimeSpan)call.Args[1]!)];

    private int Wakes() =>
        _fakes.Log.On<IAssetMovementDispatcherGrain>().Count(c => c.Method == "WakeAsync");

    /// <summary>An outbox row written <paramref name="ago"/> (10 s by default: settled).</summary>
    private void Row(long id, TimeSpan? ago = null, string? op = null) =>
        Insert(
            new AssetMovementOutboxEntity
            {
                Id = id,
                OperationId = op ?? $"op-{id}",
                OccurredAt = Start,
                AssetKind = AssetKind.Furni,
                AssetId = 100 + id,
                DefinitionId = 7,
                FromOwner = 5,
                FromKind = PlaceKind.Room,
                FromPlace = 12,
                ToOwner = 5,
                ToKind = PlaceKind.Inventory,
                Amount = 1,
                Actor = 5,
                Cause = MovementCauses.ROOM_PICKUP,
                CreatedAt = _clock.GetUtcNow().UtcDateTime - (ago ?? TimeSpan.FromSeconds(10)),
            }
        );

    private void Insert(AssetMovementOutboxEntity row)
    {
        using var db = _db.CreateDbContext();
        db.AssetMovementOutbox.Add(row);
        db.SaveChanges();
    }

    private List<AssetMovementOutboxEntity> Outbox()
    {
        using var db = _db.CreateDbContext();

        return [.. db.AssetMovementOutbox.AsNoTracking().OrderBy(x => x.Id)];
    }

    /// <summary>
    /// A consumer as a ledger plugin writes one: its cursor and the (operation, seq) pairs it
    /// applied are stored together, one transaction at a time, and a pair seen before is dropped.
    /// </summary>
    private sealed class LedgerConsumer(string name) : IAssetMovementConsumer
    {
        private readonly Lock _transaction = new();

        public string Name => name;

        public long Cursor { get; set; }

        public HashSet<(string Operation, short Seq)> Applied { get; } = [];

        public List<AssetMovementBatch> Batches { get; } = [];

        public int Delivered { get; private set; }

        public int CursorReads { get; private set; }

        public int FailuresLeft { get; set; }

        public Task<long> GetCursorAsync(CancellationToken ct)
        {
            lock (_transaction)
            {
                CursorReads++;

                return Task.FromResult(Cursor);
            }
        }

        /// <summary>When set, the next batch waits for it before it is written.</summary>
        public Task? Hold { get; set; }

        public async Task HandleAsync(AssetMovementBatch batch, CancellationToken ct)
        {
            if (Hold is { } hold)
                await hold;

            lock (_transaction)
            {
                Batches.Add(batch);

                if (FailuresLeft > 0)
                {
                    FailuresLeft--;

                    throw new InvalidOperationException("ledger write failed");
                }

                Delivered += batch.Movements.Length;

                foreach (var row in batch.Movements)
                    Applied.Add((row.Movement.OperationId, row.Movement.Seq));

                Cursor = Math.Max(Cursor, batch.Cursor);
            }
        }
    }

    /// <summary>The database, counting the contexts made from it: no context, no query.</summary>
    private sealed class CountingDb(IDbContextFactory<TurboDbContext> inner)
        : IDbContextFactory<TurboDbContext>
    {
        public int Contexts { get; private set; }

        public TurboDbContext CreateDbContext()
        {
            Contexts++;

            return inner.CreateDbContext();
        }

        public Task<TurboDbContext> CreateDbContextAsync(CancellationToken ct = default) =>
            Task.FromResult(CreateDbContext());
    }
}
