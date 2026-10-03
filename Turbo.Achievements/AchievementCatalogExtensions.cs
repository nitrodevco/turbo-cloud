using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Achievements;
using Turbo.Primitives.Achievements.Enums;

namespace Turbo.Achievements;

public static class AchievementCatalogExtensions
{
    /// <summary>
    /// Moves an achievement to another state by publishing a new revision of it through the same
    /// audited, validated import an administrator would run, so there is never a hand-edited
    /// revision number and the change is on record. Retire is <see cref="AchievementState.Archived"/>:
    /// players keep what they earned and the ones who progressed it still see it, but nothing more
    /// is awarded. Disabled hides it from everyone. With <paramref name="apply"/> false it only
    /// validates.
    /// </summary>
    public static async Task<AchievementStateChange> SetStateAsync(
        this IAchievementCatalog catalog,
        string key,
        AchievementState state,
        bool apply,
        string actor,
        string reason,
        string operationId,
        CancellationToken ct
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var current =
            catalog.Current.FirstOrDefault(x =>
                string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase)
            )
            ?? throw new InvalidOperationException($"There is no achievement with the key {key}.");
        if (current.State == state)
            return new(current.Key, state, state, current.Revision, false);
        var next = current with { Revision = current.Revision + 1, State = state };
        await catalog
            .ImportAsync([next], apply, actor, reason, operationId, ct)
            .ConfigureAwait(false);

        return new(current.Key, current.State, state, next.Revision, true);
    }
}
