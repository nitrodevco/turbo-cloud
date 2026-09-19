using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Players.Snapshots;

namespace Turbo.Primitives.Players.Grains;

public partial interface IPlayerPresenceGrain
{
    public Task OnPlayerUpdatedAsync(PlayerSummarySnapshot snapshot, CancellationToken ct);
    public Task OnFigureUpdatedAsync(PlayerSummarySnapshot snapshot, CancellationToken ct);

    /// <summary>The player's badges rank changed: the room they are in shows it on the info stand.</summary>
    public Task OnBadgesRankChangedAsync(PlayerSummarySnapshot snapshot, CancellationToken ct);
}
