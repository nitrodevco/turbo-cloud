using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Primitives.Achievements;

public interface IAchievementCatalog
{
    ImmutableArray<AchievementDefinition> Current { get; }
    string DefaultCategory => "identity";
    IDisposable RegisterSources(IEnumerable<AchievementSourceDefinition> sources);
    Task ReloadAsync(CancellationToken ct);
    Task ImportAsync(
        ImmutableArray<AchievementDefinition> definitions,
        bool apply,
        string actor,
        string reason,
        string operationId,
        CancellationToken ct
    );
}
