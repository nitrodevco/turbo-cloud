using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Players.Snapshots.Achievements;

namespace Turbo.Primitives.Players.Grains;

public interface IPlayerAchievementGrain : IGrainWithIntegerKey
{
    public Task<PlayerAchievementProgressSnapshot?> ProgressAsync(
        string achievementCode,
        int amount,
        CancellationToken ct
    );

    public Task<PlayerAchievementProgressSnapshot?> GetProgressAsync(
        string achievementCode,
        CancellationToken ct
    );

    public Task<List<PlayerAchievementProgressSnapshot>> GetAllProgressAsync(CancellationToken ct);

    public Task<int> GetTotalScoreAsync(CancellationToken ct);
}
