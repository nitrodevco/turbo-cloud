using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Achievements;
using Turbo.Primitives.Achievements;
using Turbo.Primitives.Achievements.Enums;

namespace Turbo.Tests.Support;

/// <summary>
/// A catalog with one enabled definition per core source, so every fact a producer records is
/// listened to and therefore stored. An empty catalog stores nothing, which is what the recorder
/// is meant to do when nothing listens.
/// </summary>
public sealed class ListeningAchievementCatalog : IAchievementCatalog
{
    public ImmutableArray<AchievementDefinition> Current { get; } =
    [
        .. CoreAchievementSources.All.Select(
            (source, index) =>
                new AchievementDefinition
                {
                    Id = 900000 + index,
                    Key = $"listening-{index}",
                    Revision = 1,
                    Category = "test",
                    Source = source.Key,
                    SourceVersion = source.Version,
                    Reducer = AchievementReducer.Counter,
                    Levels = [new() { Requirement = 1, BadgeCode = $"ACH_Listening{index}1" }],
                }
        ),
    ];

    public IDisposable RegisterSources(IEnumerable<AchievementSourceDefinition> sources) =>
        throw new NotSupportedException();

    public Task ReloadAsync(CancellationToken ct) => Task.CompletedTask;

    public Task ImportAsync(
        ImmutableArray<AchievementDefinition> definitions,
        bool apply,
        string actor,
        string reason,
        string operationId,
        CancellationToken ct
    ) => Task.CompletedTask;
}
