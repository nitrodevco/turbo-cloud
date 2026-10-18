using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Turbo.Database.Configuration;
using Turbo.Database.Context;
using Turbo.Database.Entities.Movements;
using Turbo.Database.Entities.Permissions;
using Turbo.Database.Extensions;
using Turbo.Database.Movements;
using Turbo.Primitives.Movements;
using Turbo.Primitives.Movements.Enums;
using Turbo.Primitives.Players.Enums.Wallet;
using Turbo.Primitives.Players.Wallet;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Database;

/// <summary>
/// The asset movement recorder (docs/movements.md): off it records nothing; on, a movement goes
/// into the caller's own save and commits or rolls back with it; an invalid one is logged and
/// dropped while the caller's change still commits; and turning recording on or off writes the
/// marker row a consumer reads the gap from.
/// </summary>
public sealed class AssetMovementRecorderTests : IDisposable
{
    private readonly SqliteDb _db = new();
    private readonly CapturingLogger<AssetMovementRecorder> _logger = new();
    private readonly TestOptionsMonitor<MovementsConfig> _config = new(new() { Enabled = false });

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task Off_RecordsNothing_AndWritesNoMarker()
    {
        var recorder = await StartedAsync(enabled: false);

        await using (var db = _db.CreateDbContext())
        {
            recorder.Record(db, Pickup());
            recorder.Record(db, [Pickup(seq: 1)]);
            await db.SaveChangesAsync();
        }

        recorder.IsRecording.Should().BeFalse();
        Outbox().Should().BeEmpty();
    }

    [Fact]
    public async Task On_TheMovementIsWrittenByTheCallersSave_AfterTheStartedMarker()
    {
        var recorder = await StartedAsync(enabled: true);

        await using (var db = _db.CreateDbContext())
        {
            recorder.Record(db, Pickup());

            // No I/O of its own: nothing is written until the caller saves.
            Movements().Should().BeEmpty();

            await using var tx = await db.Database.BeginTransactionAsync();
            db.PermissionAudit.Add(Audit());
            await db.SaveChangesAsync();
            await tx.CommitAsync();
        }

        var rows = Outbox();
        rows.Should().HaveCount(2);
        rows[0].Cause.Should().Be(MovementCauses.RECORDING_STARTED);
        rows[0].CauseRef.Should().Be(MovementCauses.MARKER_REF_SERVER);
        rows[0].Amount.Should().Be(0);

        var row = rows[1];
        row.Id.Should().BeGreaterThan(rows[0].Id);
        row.OperationId.Should().Be("op-1");
        row.Seq.Should().Be(0);
        row.AssetKind.Should().Be(AssetKind.Furni);
        row.AssetId.Should().Be(42);
        row.DefinitionId.Should().Be(7);
        row.CurrencyType.Should().BeNull();
        row.FromOwner.Should().Be(5);
        row.FromKind.Should().Be(PlaceKind.Room);
        row.FromPlace.Should().Be(10);
        row.ToOwner.Should().Be(5);
        row.ToKind.Should().Be(PlaceKind.Inventory);
        row.ToPlace.Should().Be(0);
        // The room end's position: here the tile it was picked up from.
        (row.X, row.Y, row.Z, row.Rotation, row.WallOffset)
            .Should()
            .Be((3, 4, 1.5, Rotation.East, (int?)null));
        row.Amount.Should().Be(1);
        row.Actor.Should().Be(5);
        row.Cause.Should().Be("room.pickup");
        row.CauseRef.Should().BeNull();
        row.SnapshotExtraData.Should().BeNull();
        _logger.AtLeast(LogLevel.Warning).Should().BeEmpty();
    }

    [Fact]
    public async Task On_ACallerThatRollsBack_LeavesNoMovement()
    {
        var recorder = await StartedAsync(enabled: true);

        await using (var db = _db.CreateDbContext())
        {
            await using var tx = await db.Database.BeginTransactionAsync();
            recorder.Record(db, Pickup());
            db.PermissionAudit.Add(Audit());
            await db.SaveChangesAsync();
            await tx.RollbackAsync();
        }

        Movements().Should().BeEmpty();
        Audits().Should().Be(0);
    }

    [Fact]
    public async Task On_CurrencyAndSnapshot_AreStoredAsGiven()
    {
        var recorder = await StartedAsync(enabled: true);
        var cause = new MovementCause("op-2", "catalog.purchase", "offer-9", 5);
        var now = DateTime.UtcNow;

        await using (var db = _db.CreateDbContext())
        {
            recorder.Record(
                db,
                [
                    AssetMovement.For(
                        cause,
                        0,
                        now,
                        new AssetRef(AssetKind.Currency, 0, 0, CurrencyKind.ActivityPoints(5)),
                        new AssetPlace(5, PlaceKind.Wallet, 0, null),
                        AssetPlace.Nowhere,
                        amount: 25
                    ),
                    AssetMovement.For(
                        cause,
                        1,
                        now,
                        new AssetRef(AssetKind.Furni, 43, 7, null),
                        AssetPlace.Nowhere,
                        new AssetPlace(5, PlaceKind.Inventory, 0, null),
                        snapshot: new AssetSnapshot(7, null)
                    ),
                ]
            );
            await db.SaveChangesAsync();
        }

        var rows = Movements();
        rows.Should().HaveCount(2);
        rows[0].CurrencyType.Should().Be(CurrencyType.ActivityPoints);
        rows[0].ActivityPointType.Should().Be(5);
        rows[0].Amount.Should().Be(25);
        rows[0].CauseRef.Should().Be("offer-9");
        rows[0].SnapshotExtraData.Should().BeNull();
        // A snapshot without extra data still reads as a snapshot.
        rows[1].SnapshotExtraData.Should().Be(string.Empty);
        rows[1].Seq.Should().Be(1);
    }

    public static TheoryData<string> InvalidMovements =>
        [
            "empty operation",
            "long operation",
            "negative seq",
            "local time",
            "future",
            "empty cause",
            "long cause",
            "marker cause",
            "pruned marker cause",
            "long cause ref",
            "unknown kind",
            "temporary furni",
            "item amount",
            "currency without kind",
            "zero currency",
            "moves nothing",
            "snapshot of another definition",
        ];

    [Theory]
    [MemberData(nameof(InvalidMovements))]
    public async Task On_AnInvalidMovementIsLoggedAndDropped_AndTheCallerStillCommits(string name)
    {
        var recorder = await StartedAsync(enabled: true);

        await using (var db = _db.CreateDbContext())
        {
            var act = () => recorder.Record(db, [Invalid(name), Pickup(seq: 1)]);

            act.Should().NotThrow();
            db.PermissionAudit.Add(Audit());
            await db.SaveChangesAsync();
        }

        Audits().Should().Be(1);
        Movements().Select(x => x.Seq).Should().Equal(1);
        _logger.AtLeast(LogLevel.Error).Should().ContainSingle();
    }

    [Fact]
    public async Task On_NoDatabaseContext_IsLoggedNotThrown()
    {
        var recorder = await StartedAsync(enabled: true);

        var act = () => recorder.Record(null!, Pickup());

        act.Should().NotThrow();
        _logger.AtLeast(LogLevel.Error).Should().ContainSingle();
    }

    [Fact]
    public async Task Toggling_WritesOneMarkerEachWay_AndStopsAtOnce()
    {
        var recorder = await StartedAsync(enabled: false);

        // On, but recording waits for its marker, so nothing can come before it.
        _config.Set(new() { Enabled = true });
        recorder.IsRecording.Should().BeFalse();
        await recorder.SyncAsync(MovementCauses.MARKER_REF_SETTING, CancellationToken.None);
        await recorder.SyncAsync(MovementCauses.MARKER_REF_SETTING, CancellationToken.None);
        recorder.IsRecording.Should().BeTrue();

        // Off stops at once, before the marker is written.
        _config.Set(new() { Enabled = false });
        recorder.IsRecording.Should().BeFalse();
        await using (var db = _db.CreateDbContext())
        {
            recorder.Record(db, Pickup());
            await db.SaveChangesAsync();
        }
        await recorder.SyncAsync(MovementCauses.MARKER_REF_SETTING, CancellationToken.None);

        Outbox()
            .Select(x => (x.Cause, x.CauseRef))
            .Should()
            .Equal(
                (MovementCauses.RECORDING_STARTED, MovementCauses.MARKER_REF_SETTING),
                (MovementCauses.RECORDING_STOPPED, MovementCauses.MARKER_REF_SETTING)
            );
    }

    [Fact]
    public async Task TheServerStopping_WritesTheStoppedMarker_OnlyWhileRecording()
    {
        var on = await StartedAsync(enabled: true);
        await on.StopAsync(CancellationToken.None);
        on.IsRecording.Should().BeFalse();

        var off = await StartedAsync(enabled: false);
        await off.StopAsync(CancellationToken.None);

        Outbox()
            .Select(x => (x.Cause, x.CauseRef))
            .Should()
            .Equal(
                (MovementCauses.RECORDING_STARTED, MovementCauses.MARKER_REF_SERVER),
                (MovementCauses.RECORDING_STOPPED, MovementCauses.MARKER_REF_SERVER)
            );
    }

    [Fact]
    public async Task TheHost_RegistersOneRecorder_OffByDefault_AndASettingChangeStartsIt()
    {
        var builder = Host.CreateEmptyApplicationBuilder(new());
        builder.Configuration.AddInMemoryCollection();
        builder.Services.AddLogging();
        builder.Services.AddSingleton<IDbContextFactory<TurboDbContext>>(_db);
        builder.Services.AddTurboMovements(builder);
        await using var sp = builder.Services.BuildServiceProvider();

        var recorder = sp.GetRequiredService<IAssetMovementRecorder>();
        sp.GetServices<IHostedService>().Should().ContainSingle().Which.Should().BeSameAs(recorder);
        sp.GetRequiredService<IOptionsMonitor<MovementsConfig>>()
            .CurrentValue.Enabled.Should()
            .BeFalse();

        var hosted = (AssetMovementRecorder)recorder;
        await hosted.StartAsync(CancellationToken.None);
        recorder.IsRecording.Should().BeFalse();

        // What a panel change does: the configuration reloads and the monitor tells its listeners.
        builder.Configuration["Turbo:Movements:Enabled"] = "true";
        ((IConfigurationRoot)builder.Configuration).Reload();
        await CountWait.ForAsync(() => Outbox().Count, 1);

        recorder.IsRecording.Should().BeTrue();
        Outbox().Single().CauseRef.Should().Be(MovementCauses.MARKER_REF_SETTING);
        await hosted.StopAsync(CancellationToken.None);
    }

    private async Task<AssetMovementRecorder> StartedAsync(bool enabled)
    {
        _config.Set(new() { Enabled = enabled });
        var recorder = new AssetMovementRecorder(_config, _db, _logger);
        await recorder.StartAsync(CancellationToken.None);

        return recorder;
    }

    private static AssetMovement Pickup(short seq = 0) =>
        new()
        {
            OperationId = "op-1",
            Seq = seq,
            OccurredAtUtc = DateTime.UtcNow,
            Asset = new AssetRef(AssetKind.Furni, 42, 7, null),
            From = new AssetPlace(
                5,
                PlaceKind.Room,
                10,
                new RoomPosition(3, 4, 1.5, Rotation.East, null)
            ),
            To = new AssetPlace(5, PlaceKind.Inventory, 0, null),
            ActorPlayerId = 5,
            Cause = "room.pickup",
        };

    private static AssetMovement Invalid(string name)
    {
        var valid = Pickup();
        var coins = valid with
        {
            Asset = new AssetRef(AssetKind.Currency, 0, 0, CurrencyKind.Credits),
            From = new AssetPlace(5, PlaceKind.Wallet, 0, null),
            To = AssetPlace.Nowhere,
            Amount = 10,
        };

        return name switch
        {
            "empty operation" => valid with { OperationId = " " },
            "long operation" => valid with { OperationId = new string('o', 161) },
            "negative seq" => valid with { Seq = -1 },
            "local time" => valid with
            {
                OccurredAtUtc = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Local),
            },
            "future" => valid with { OccurredAtUtc = DateTime.UtcNow.AddMinutes(5) },
            "empty cause" => valid with { Cause = "" },
            "long cause" => valid with { Cause = new string('c', 49) },
            "marker cause" => valid with { Cause = MovementCauses.RECORDING_STARTED },
            "pruned marker cause" => valid with { Cause = MovementCauses.OUTBOX_PRUNED },
            "long cause ref" => valid with { CauseRef = new string('r', 65) },
            "unknown kind" => valid with { To = valid.To with { Kind = (PlaceKind)99 } },
            "temporary furni" => valid with { Asset = valid.Asset with { Id = -3 } },
            "item amount" => valid with { Amount = 2 },
            "currency without kind" => coins with { Asset = coins.Asset with { Currency = null } },
            "zero currency" => coins with { Amount = 0 },
            "moves nothing" => valid with { To = valid.From },
            "snapshot of another definition" => valid with { Snapshot = new AssetSnapshot(8, "1") },
            _ => throw new ArgumentOutOfRangeException(nameof(name), name, null),
        };
    }

    private static PermissionAuditEntity Audit() =>
        new()
        {
            TargetType = default,
            TargetId = 1,
            Action = default,
            Subject = "test",
        };

    private List<AssetMovementOutboxEntity> Outbox()
    {
        using var db = _db.CreateDbContext();

        return db.AssetMovementOutbox.AsNoTracking().OrderBy(x => x.Id).ToList();
    }

    private List<AssetMovementOutboxEntity> Movements() =>
        Outbox()
            .Where(x =>
                x.Cause
                    is not (MovementCauses.RECORDING_STARTED or MovementCauses.RECORDING_STOPPED)
            )
            .ToList();

    private int Audits()
    {
        using var db = _db.CreateDbContext();

        return db.PermissionAudit.Count();
    }
}
