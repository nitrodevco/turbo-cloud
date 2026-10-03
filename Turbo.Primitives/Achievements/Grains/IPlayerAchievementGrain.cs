using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Achievements.Snapshots;

namespace Turbo.Primitives.Achievements.Grains;

/// <summary>Owns progression and frozen awards; recovery uses this same boundary offline.</summary>
public interface IPlayerAchievementGrain : IGrainWithIntegerKey
{
    Task<ImmutableArray<AchievementSnapshot>> GetAchievementsAsync(CancellationToken ct);
    Task<ImmutableArray<AchievementAwardStatusSnapshot>> GetPendingAwardsAsync(
        CancellationToken ct
    );
    Task ProcessAsync(CancellationToken ct);
    Task ReconcileAsync(CancellationToken ct);
    Task AdvanceAsync(
        int achievementId,
        long progress,
        string actor,
        string reason,
        string operationId,
        CancellationToken ct
    );
    Task RetryAsync(CancellationToken ct);
    Task AdministerAsync(
        bool retryOnly,
        string actor,
        string reason,
        string operationId,
        CancellationToken ct
    );
}
