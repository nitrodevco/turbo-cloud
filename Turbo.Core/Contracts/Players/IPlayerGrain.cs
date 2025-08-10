using System.Threading;
using System.Threading.Tasks;
using Orleans;

namespace Turbo.Core.Contracts.Players;

public interface IPlayerGrain : IGrainWithIntegerKey
{
    public Task SetName(string name, CancellationToken ct = default);
    public Task<PlayerSummary> GetAsync();
}