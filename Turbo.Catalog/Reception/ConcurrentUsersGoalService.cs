using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Catalog.Configuration;
using Turbo.Primitives.Hotel;
using Turbo.Primitives.Hotel.Enums;
using Turbo.Primitives.Hotel.Snapshots;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;

namespace Turbo.Catalog.Reception;

/// <summary>
/// The concurrent users goal (<see cref="IConcurrentUsersGoalService"/>), set by
/// <see cref="ReceptionConfig.ConcurrentUsersGoal"/>. The players online are those connected to
/// this host, as the panel's dashboard counts them, less the system accounts among them. Once the count reaches the goal the goal stays
/// reached until the host restarts, so a player who has not taken the reward yet keeps it to take
/// when the count drops again. Whether a player took it is whether they own the reward badge.
/// </summary>
public sealed class ConcurrentUsersGoalService(
    IOptions<CatalogConfig> config,
    IGrainFactory grainFactory,
    ISessionGateway sessionGateway,
    ILogger<ConcurrentUsersGoalService> logger
) : IConcurrentUsersGoalService
{
    private readonly ReceptionConfig _config = config.Value.Reception;

    private volatile bool _reached;

    public async Task<ConcurrentUsersGoalProgressSnapshot> GetProgressAsync(
        PlayerId player,
        CancellationToken ct
    )
    {
        // Players are counted, not the system accounts the hotel or its plugins keep online.
        var online = sessionGateway.GetOnlinePlayerIds();
        var systemAccounts = await grainFactory
            .GetPlayerDirectoryGrain()
            .GetSystemAccountIdsAsync(ct)
            .ConfigureAwait(false);
        var userCount = systemAccounts.IsDefaultOrEmpty
            ? online.Count
            : online.Count - systemAccounts.Count(online.Contains);
        var goal = _config.ConcurrentUsersGoal;

        if (goal <= 0)
            return Progress(ConcurrentUsersGoalState.Disabled, userCount, 0);

        if (userCount >= goal)
            _reached = true;

        if (!_reached)
            return Progress(ConcurrentUsersGoalState.Active, userCount, goal);

        var badge = _config.ConcurrentUsersRewardBadge;
        var rewarded =
            badge.Length == 0
            || await grainFactory
                .GetPlayerBadgeGrain(player)
                .HasBadgeAsync(badge, ct)
                .ConfigureAwait(false);

        return Progress(
            rewarded ? ConcurrentUsersGoalState.Rewarded : ConcurrentUsersGoalState.Redeem,
            userCount,
            goal
        );
    }

    public async Task ClaimRewardAsync(PlayerId player, CancellationToken ct)
    {
        var progress = await GetProgressAsync(player, ct).ConfigureAwait(false);

        if (progress.State != ConcurrentUsersGoalState.Redeem)
        {
            logger.LogWarning(
                "Refused the concurrent users reward to player {PlayerId}: the goal is {State}",
                player,
                progress.State
            );

            return;
        }

        await grainFactory
            .GetPlayerBadgeGrain(player)
            .GiveBadgeAsync(_config.ConcurrentUsersRewardBadge, ct)
            .ConfigureAwait(false);
    }

    private static ConcurrentUsersGoalProgressSnapshot Progress(
        ConcurrentUsersGoalState state,
        int userCount,
        int goal
    ) =>
        new()
        {
            State = state,
            UserCount = userCount,
            UserCountGoal = goal,
        };
}
