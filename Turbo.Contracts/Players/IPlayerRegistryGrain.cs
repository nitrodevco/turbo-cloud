using System.Threading.Tasks;
using Orleans;
using Turbo.Contracts.Shared;

namespace Turbo.Contracts.Players;

public interface IPlayerRegistryGrain : IGrainWithStringKey
{
    public Task<EnsureResult<PlayerSummary>> EnsureExistsAsync(bool createIfMissing = false);

    public Task<bool> ExistsAsync();
}