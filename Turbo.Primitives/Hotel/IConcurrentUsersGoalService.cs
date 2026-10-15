using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Hotel.Snapshots;
using Turbo.Primitives.Players;

namespace Turbo.Primitives.Hotel;

/// <summary>
/// The reception's concurrent users goal: once enough players are online at the same time, each
/// player may take the reward badge.
/// </summary>
public interface IConcurrentUsersGoalService
{
    /// <summary>Where the goal stands for the player.</summary>
    public Task<ConcurrentUsersGoalProgressSnapshot> GetProgressAsync(
        PlayerId player,
        CancellationToken ct
    );

    /// <summary>Gives the player the reward badge when the goal is reached and it is theirs to take.</summary>
    public Task ClaimRewardAsync(PlayerId player, CancellationToken ct);
}
