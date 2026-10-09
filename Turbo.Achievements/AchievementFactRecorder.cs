using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Turbo.Database.Achievements;
using Turbo.Database.Context;
using Turbo.Database.Entities.Achievements;
using Turbo.Primitives.Achievements;
using Turbo.Primitives.Players;

namespace Turbo.Achievements;

/// <param name="listeners">Told of every valid fact before the catalog decides whether to keep it.</param>
public sealed class AchievementFactRecorder(
    IAchievementCatalog catalog,
    IEnumerable<IAchievementFactListener>? listeners = null
) : IAchievementFactRecorder
{
    private readonly IAchievementFactListener[] _listeners = listeners?.ToArray() ?? [];

    public void Record(TurboDbContext db, PlayerId playerId, AchievementFact fact)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(fact);
        if (
            playerId <= 0
            || string.IsNullOrWhiteSpace(fact.OperationId)
            || fact.OperationId.Length > 160
            || fact.OccurredAtUtc.Kind != DateTimeKind.Utc
            || fact.Amount < 0
            || fact.Value.Length > 512
            || fact.Version <= 0
            || fact.OccurredAtUtc > DateTime.UtcNow.AddMinutes(1)
        )
            throw new ArgumentException("Invalid authoritative achievement fact.", nameof(fact));
        foreach (var listener in _listeners)
            listener.OnFactRecorded(playerId, fact);
        var targets = catalog
            .Current.Where(x =>
                x.Accrues(fact.OccurredAtUtc)
                && x.Source == fact.Source
                && x.SourceVersion == fact.Version
                && x.Matches(fact)
            )
            .ToArray();
        foreach (var target in targets)
        {
            if (
                target.Reducer == Turbo.Primitives.Achievements.Enums.AchievementReducer.Distinct
                && string.IsNullOrWhiteSpace(target.CountedValue(fact))
            )
                throw new ArgumentException("Distinct facts require a stable value.", nameof(fact));
            if (
                target.Reducer
                    == Turbo.Primitives.Achievements.Enums.AchievementReducer.ElapsedSeconds
                && (
                    fact.IntervalStartUtc is not { Kind: DateTimeKind.Utc } start
                    || fact.IntervalEndUtc is not { Kind: DateTimeKind.Utc } end
                    || start > end
                    || end > fact.OccurredAtUtc
                    || string.IsNullOrWhiteSpace(fact.SessionId)
                )
            )
                throw new ArgumentException(
                    "Elapsed facts require durable UTC intervals and a session identity.",
                    nameof(fact)
                );
        }
        // A fact nothing listens to is never needed: bindings are frozen when a fact is admitted, so
        // a definition added later would not have counted it anyway. Skipping it keeps a hotel with
        // a small catalog from storing, say, an online interval per player every half minute.
        if (targets.Length == 0)
            return;
        db.AchievementFacts.Add(
            new AchievementFactEntity
            {
                PlayerId = playerId.Value,
                Source = fact.Source,
                OperationId = fact.OperationId,
                OccurredAtUtc = fact.OccurredAtUtc,
                FactJson = JsonSerializer.Serialize(fact),
                BindingsJson = JsonSerializer.Serialize(
                    targets.Select(x => new AchievementBinding(x.Id, x.Revision))
                ),
            }
        );
    }
}
