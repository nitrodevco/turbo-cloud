using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Primitives.Players.Grains;

public partial interface IPlayerPresenceGrain
{
    public Task OnRequestWardrobeAsync(CancellationToken ct);
}
