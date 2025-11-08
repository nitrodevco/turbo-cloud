using System.Threading.Tasks;
using Orleans;

namespace Turbo.Primitives.Players;

public interface IPlayerEndpointGrain : IGrainWithIntegerKey
{
    public Task BindConnectionAsync(string connectionId);
    public Task UnbindConnectionAsync(string connectionId);
}
