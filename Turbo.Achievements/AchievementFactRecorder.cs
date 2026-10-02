using System;
using System.Linq;
using System.Text.Json;
using Turbo.Database.Achievements;
using Turbo.Database.Context;
using Turbo.Database.Entities.Achievements;
using Turbo.Primitives.Achievements;
using Turbo.Primitives.Players;

namespace Turbo.Achievements;

public sealed class AchievementFactRecorder(IAchievementCatalog catalog) : IAchievementFactRecorder
{
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
        var targets = catalog
            .Current.Where(x =>
                x.Enabled
                && !x.Archived
                && x.Source == fact.Source
                && x.SourceVersion == fact.Version
            )
            .ToArray();
        foreach (var target in targets)
        {
            if (
                target.Reducer == Turbo.Primitives.Achievements.Enums.AchievementReducer.Distinct
                && string.IsNullOrWhiteSpace(fact.Value)
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
