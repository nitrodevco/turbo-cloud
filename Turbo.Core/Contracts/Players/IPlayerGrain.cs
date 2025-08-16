namespace Turbo.Core.Contracts.Players;

using System.Threading;
using System.Threading.Tasks;

using Orleans;

public interface IPlayerGrain : IGrainWithIntegerKey
{
    public Task<long> GetPlayerId();

    public Task SetName(string name, CancellationToken ct = default);

    public Task<PlayerSummary> GetAsync();
}
