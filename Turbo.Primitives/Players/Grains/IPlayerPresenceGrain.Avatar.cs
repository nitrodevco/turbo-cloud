using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Orleans.Snapshots.Players;

namespace Turbo.Primitives.Players.Grains;

public partial interface IPlayerPresenceGrain
{
    public Task OnFigureUpdatedAsync(PlayerSummarySnapshot snapshot, CancellationToken ct);
}
