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
            if (!revisions.TryGetValue(key, out var definition))
            {
                definition = _catalog.Current.FirstOrDefault(x =>
                    x.Id == key.Item1 && x.Revision == key.Item2
                );
                if (definition is null)
                {
                    var row = await db
                        .AchievementDefinitions.AsNoTracking()
                        .SingleOrDefaultAsync(
                            x => x.AchievementId == key.Item1 && x.Revision == key.Item2,
                            ct
                        );
                    if (row is null)
                        throw new InvalidOperationException(
                            $"Achievement {key.Item1} revision {key.Item2} is not stored."
                        );
                    definition =
                        JsonSerializer.Deserialize<AchievementDefinition>(row.DefinitionJson)
                        ?? throw new InvalidOperationException("Empty definition.");
                }
                revisions[key] = definition;
            }
            definitions.Add(definition);
        }
        return definitions;
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
