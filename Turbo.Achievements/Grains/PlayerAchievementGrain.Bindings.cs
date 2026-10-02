using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Context;
using Turbo.Database.Entities.Achievements;
using Turbo.Primitives.Achievements;

namespace Turbo.Achievements.Grains;

internal sealed partial class PlayerAchievementGrain
{
    /// <summary>
    /// A fact binds to immutable definition revisions by key. The current catalog answers the
    /// common case; an older revision is read from storage.
    /// </summary>
    private async Task<List<AchievementDefinition>> ResolveBindingsAsync(
        TurboDbContext db,
        string bindingsJson,
        Dictionary<(int, int), AchievementDefinition> revisions,
        CancellationToken ct
    )
    {
        using var document = JsonDocument.Parse(bindingsJson);
        var definitions = new List<AchievementDefinition>();
        foreach (var element in document.RootElement.EnumerateArray())
        {
            var key = (
                element.GetProperty(nameof(AchievementBinding.Id)).GetInt32(),
                element.GetProperty(nameof(AchievementBinding.Revision)).GetInt32()
            );
            definitions.Add(await ResolveRevisionAsync(db, key.Item1, key.Item2, revisions, ct));
        }
        return definitions;
    }

    /// <summary>
    /// One immutable definition revision: the current catalog answers the common case, an older
    /// one is read from storage. Facts and open awards both freeze a revision this way.
    /// </summary>
    private async Task<AchievementDefinition> ResolveRevisionAsync(
        TurboDbContext db,
        int achievementId,
        int revision,
        Dictionary<(int, int), AchievementDefinition> revisions,
        CancellationToken ct
    )
    {
        var key = (achievementId, revision);
        if (revisions.TryGetValue(key, out var definition))
            return definition;
        definition = _catalog.Current.FirstOrDefault(x =>
            x.Id == achievementId && x.Revision == revision
        );
        if (definition is null)
        {
            var row = await db
                .AchievementDefinitions.AsNoTracking()
                .SingleOrDefaultAsync(
                    x => x.AchievementId == achievementId && x.Revision == revision,
                    ct
                );
            if (row is null)
                throw new InvalidOperationException(
                    $"Achievement {achievementId} revision {revision} is not stored."
                );
            definition = AchievementDefinitionJson.Read(row.DefinitionJson);
        }
        revisions[key] = definition;
        return definition;
    }

    /// <summary>
    /// Distinct values live in their own table, so a fact costs one key lookup rather than a
    /// rewrite of every value the player has contributed.
    /// </summary>
    private async Task ApplyDistinctAsync(
        TurboDbContext db,
        AchievementProgressEntity progress,
        AchievementFact fact,
        CancellationToken ct
    )
    {
        var added =
            !string.IsNullOrWhiteSpace(fact.Value)
            && await db.AchievementDistinctValues.FindAsync(
                [progress.PlayerId, progress.AchievementId, fact.Value],
                ct
            )
                is null;
        AchievementReducerEngine.ApplyDistinct(progress, fact, added, _config.MaxDistinctValues);
        if (added)
            db.AchievementDistinctValues.Add(
                new()
                {
                    PlayerId = progress.PlayerId,
                    AchievementId = progress.AchievementId,
                    Value = fact.Value,
                }
            );
    }
}
