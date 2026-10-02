using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Turbo.Achievements.Grains;

internal sealed partial class PlayerAchievementGrain
{
    public async Task AdvanceAsync(
        int achievementId,
        long progress,
        string actor,
        string reason,
        string operationId,
        CancellationToken ct
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actor);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
        if (operationId.Length > 160)
            throw new ArgumentException("Operation id too long.", nameof(operationId));
        var definition = _catalog.Current.Single(x => x.Id == achievementId);
        if (definition.Reducer == Turbo.Primitives.Achievements.Enums.AchievementReducer.Rank)
            throw new InvalidOperationException(
                "Rank criteria reconcile from authoritative rankings."
            );
        await using (var db = await _database.CreateDbContextAsync(ct))
        {
            var request = JsonSerializer.Serialize(
                new
                {
                    Action = "advance",
                    PlayerId = _state.PlayerId.Value,
                    AchievementId = achievementId,
                    Progress = progress,
                }
            );
            var receipt = await db.AchievementAudit.SingleOrDefaultAsync(
                x => x.OperationId == operationId,
                ct
            );
            if (receipt is not null)
            {
                if (
                    receipt.Actor != actor
                    || receipt.Reason != reason
                    || receipt.RequestJson != request
                )
                    throw new InvalidOperationException("Audit operation id collision.");
                return;
            }
            var row = await db.AchievementProgress.FindAsync(
                [_state.PlayerId.Value, achievementId],
                ct
            );
            if (row is null)
            {
                row = new() { PlayerId = _state.PlayerId.Value, AchievementId = achievementId };
                db.AchievementProgress.Add(row);
            }
            if (progress < row.Value)
                throw new InvalidOperationException(
                    "Achievement administration only advances progress."
                );
            var before = JsonSerializer.Serialize(row);
            if (
                definition.Reducer
                is Turbo.Primitives.Achievements.Enums.AchievementReducer.Distinct
                    or Turbo.Primitives.Achievements.Enums.AchievementReducer.ElapsedSeconds
            )
                row.ForwardAdjustment = checked(row.ForwardAdjustment + progress - row.Value);
            row.Value = progress;
            await CreateAwardsAsync(db, definition, row, DateTime.UtcNow, ct);
            db.AchievementAudit.Add(
                new()
                {
                    OperationId = operationId,
                    Actor = actor,
                    Reason = reason,
                    RequestJson = request,
                    OccurredAtUtc = DateTime.UtcNow,
                    BeforeJson = before,
                    AfterJson = JsonSerializer.Serialize(row),
                }
            );
            await db.SaveChangesAsync(ct);
        }
        await ProcessAsync(ct);
    }

    public async Task AdministerAsync(
        bool retryOnly,
        string actor,
        string reason,
        string operationId,
        CancellationToken ct
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actor);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
        if (operationId.Length > 160)
            throw new ArgumentException("Operation id too long.", nameof(operationId));
        await using var db = await _database.CreateDbContextAsync(ct);
        var audit = await db.AchievementAudit.SingleOrDefaultAsync(
            x => x.OperationId == operationId,
            ct
        );
        var request = JsonSerializer.Serialize(
            new { Action = retryOnly ? "retry" : "reconcile", PlayerId = _state.PlayerId.Value }
        );
        if (
            audit is not null
            && (audit.Actor != actor || audit.Reason != reason || audit.RequestJson != request)
        )
            throw new InvalidOperationException("Audit operation id collision.");
        if (audit is not null && audit.AfterJson != "pending")
            return;
        if (audit is null)
        {
            audit = new()
            {
                OperationId = operationId,
                Actor = actor,
                Reason = reason,
                RequestJson = request,
                OccurredAtUtc = DateTime.UtcNow,
                BeforeJson = JsonSerializer.Serialize(
                    await db
                        .AchievementProgress.AsNoTracking()
                        .Where(x => x.PlayerId == _state.PlayerId.Value)
                        .ToListAsync(ct)
                ),
                AfterJson = "pending",
            };
            db.AchievementAudit.Add(audit);
            await db.SaveChangesAsync(ct);
        }
        if (retryOnly)
            await ProcessAsync(ct);
        else
            await ReconcileCoreAsync(force: true, ct);
        audit.AfterJson = JsonSerializer.Serialize(
            await db
                .AchievementProgress.AsNoTracking()
                .Where(x => x.PlayerId == _state.PlayerId.Value)
                .ToListAsync(ct)
        );
        await db.SaveChangesAsync(ct);
    }
}
