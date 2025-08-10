using System.Threading.Tasks;
using Orleans;

namespace Turbo.Contracts.Players;

public interface IPlayerGrain : IGrainWithIntegerKey
{
    public Task<PlayerSummary> GetAsync();
}