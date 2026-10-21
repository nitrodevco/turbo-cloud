using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Turbo.Database.Configuration;
using Turbo.Database.Context;
using Turbo.Database.Extensions;
using Turbo.Primitives.Movements;
using Turbo.Primitives.Movements.Enums;
using Turbo.Primitives.Orleans;

namespace Turbo.Database.Movements;

/// <summary>
/// The movement recorder, and the switch behind it: as a hosted service it writes the
/// <c>recording.started</c> row when the server starts with recording on or it is turned on, and
/// the <c>recording.stopped</c> row when it is turned off or the server stops. Recording starts
/// only once its row is written, so every movement of a recording comes after the row that opens
/// it; it stops as soon as the setting says so.
/// </summary>
public sealed class AssetMovementRecorder(
    IOptionsMonitor<MovementsConfig> config,
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    ILogger<AssetMovementRecorder> logger
) : IAssetMovementRecorder, IHostedService, IDisposable
{
    private readonly SemaphoreSlim _switchLock = new(1, 1);
    private IDisposable? _configChanges;
    private volatile bool _started;

    public bool IsRecording => _started && config.CurrentValue.Enabled;

    public void Record(TurboDbContext db, AssetMovement movement)
    {
        if (!IsRecording)
            return;

        Add(db, movement, DateTime.UtcNow);
    }

    public void Record(TurboDbContext db, IReadOnlyList<AssetMovement> movements)
    {
        if (!IsRecording || movements is null)
            return;

        var now = DateTime.UtcNow;

        foreach (var movement in movements)
            Add(db, movement, now);
    }

    public async Task StartAsync(CancellationToken ct)
    {
        _configChanges = config.OnChange(
            (_, _) =>
                SyncAsync(MovementCauses.MARKER_REF_SETTING, CancellationToken.None)
                    .LogAndForget(logger, "switch asset movement recording")
        );

        await SyncAsync(MovementCauses.MARKER_REF_SERVER, ct).ConfigureAwait(false);
    }

    public async Task StopAsync(CancellationToken ct)
    {
        _configChanges?.Dispose();
        _configChanges = null;

        await _switchLock.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            if (!_started)
                return;

            _started = false;
            await WriteMarkerAsync(
                    MovementCauses.RECORDING_STOPPED,
                    MovementCauses.MARKER_REF_SERVER,
                    ct
                )
                .ConfigureAwait(false);
        }
        finally
        {
            _switchLock.Release();
        }
    }

    /// <summary>
    /// Brings recording in line with <c>Turbo:Movements:Enabled</c>, writing the marker row of a
    /// change; <paramref name="causeRef"/> says what changed it. Does nothing when it already is.
    /// </summary>
    public async Task SyncAsync(string causeRef, CancellationToken ct)
    {
        await _switchLock.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            var enabled = config.CurrentValue.Enabled;

            if (enabled == _started)
                return;

            if (!enabled)
            {
                _started = false;
                await WriteMarkerAsync(MovementCauses.RECORDING_STOPPED, causeRef, ct)
                    .ConfigureAwait(false);

                return;
            }

            await WriteMarkerAsync(MovementCauses.RECORDING_STARTED, causeRef, ct)
                .ConfigureAwait(false);
            _started = true;
        }
        finally
        {
            _switchLock.Release();
        }
    }

    public void Dispose()
    {
        _configChanges?.Dispose();
        _switchLock.Dispose();
    }

    // A recording without its marker is still worth more than no recording: a failed marker is
    // logged and recording goes on as the setting says.
    private async Task WriteMarkerAsync(string cause, string causeRef, CancellationToken ct)
    {
        try
        {
            var db = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
            await using var dbScope = db.ConfigureAwait(false);

            db.AssetMovementOutbox.Add(
                MovementEntityExtensions.ToMarkerEntity(cause, causeRef, DateTime.UtcNow)
            );
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            logger.LogInformation(
                "Asset movement recording marker {Cause} written ({CauseRef})",
                cause,
                causeRef
            );
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(
                ex,
                "Failed to write the asset movement marker {Cause} ({CauseRef})",
                cause,
                causeRef
            );
        }
    }

    // Never throws: a movement that threw would fail the save of the change it describes (a room's
    // furniture flush, a trade), and the change matters more than its record.
    private void Add(TurboDbContext db, AssetMovement? movement, DateTime nowUtc)
    {
        try
        {
            var reason = db is null ? "no database context" : Invalid(movement, nowUtc);

            if (reason is not null)
            {
                logger.LogError(
                    "Dropped an invalid asset movement ({Reason}): operation {OperationId} seq {Seq} cause {Cause} asset {AssetKind} {AssetId}",
                    reason,
                    movement?.OperationId,
                    movement?.Seq,
                    movement?.Cause,
                    movement?.Asset.Kind,
                    movement?.Asset.Id
                );

                return;
            }

            db!.AssetMovementOutbox.Add(movement!.ToOutboxEntity(nowUtc));
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to record asset movement: operation {OperationId} seq {Seq} cause {Cause}",
                movement?.OperationId,
                movement?.Seq,
                movement?.Cause
            );
        }
    }

    /// <summary>Why <paramref name="movement"/> cannot be stored, or null when it can.</summary>
    private static string? Invalid(AssetMovement? movement, DateTime nowUtc)
    {
        if (movement is null)
            return "no movement";

        if (
            string.IsNullOrWhiteSpace(movement.OperationId)
            || movement.OperationId.Length > AssetMovement.MAX_OPERATION_ID_LENGTH
        )
            return "operation id empty or too long";

        if (movement.Seq < 0)
            return "negative seq";

        // A minute of clock skew between silos, as achievement facts allow.
        if (
            movement.OccurredAtUtc.Kind != DateTimeKind.Utc
            || movement.OccurredAtUtc > nowUtc.AddMinutes(1)
        )
            return "occurred-at not UTC or in the future";

        if (
            string.IsNullOrWhiteSpace(movement.Cause)
            || movement.Cause.Length > AssetMovement.MAX_CAUSE_LENGTH
        )
            return "cause empty or too long";

        if (
            movement.Cause
            is MovementCauses.RECORDING_STARTED
                or MovementCauses.RECORDING_STOPPED
                or MovementCauses.OUTBOX_PRUNED
        )
            return "marker cause";

        if (movement.CauseRef?.Length > AssetMovement.MAX_CAUSE_REF_LENGTH)
            return "cause ref too long";

        if (
            !Enum.IsDefined(movement.Asset.Kind)
            || !Enum.IsDefined(movement.From.Kind)
            || !Enum.IsDefined(movement.To.Kind)
        )
            return "unknown asset or place kind";

        if (movement.ActorPlayerId < 0)
            return "negative actor";

        if (movement.Asset.Kind == AssetKind.Currency)
        {
            if (movement.Asset.Currency is null)
                return "currency without a currency kind";

            if (movement.Amount <= 0)
                return "currency amount not positive";
        }
        else
        {
            // Temporary furni have negative ids and no row: they are never movements.
            if (movement.Asset.Id <= 0)
                return "asset id not positive";

            if (movement.Asset.Currency is not null)
                return "currency kind on an item";

            if (movement.Amount != 1)
                return "item amount not 1";
        }

        if (
            movement.From.OwnerPlayerId == movement.To.OwnerPlayerId
            && movement.From.Kind == movement.To.Kind
            && movement.From.PlaceId == movement.To.PlaceId
        )
            return "moves nothing";

        if (
            movement.Snapshot is { } snapshot
            && snapshot.DefinitionId != movement.Asset.DefinitionId
        )
            return "snapshot of another definition";

        return null;
    }
}
