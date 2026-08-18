using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Players.Snapshots.Achievements;

namespace Turbo.Primitives.Players.Grains;

public partial interface IPlayerPresenceGrain
{
    public Task OnAchievementProgressAsync(
        PlayerAchievementProgressSnapshot snapshot,
        CancellationToken ct
    );

    public Task OnAchievementLevelUpAsync(AchievementLevelUpSnapshot snapshot, CancellationToken ct);
}
