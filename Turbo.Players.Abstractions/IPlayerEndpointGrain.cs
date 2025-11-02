using System.Threading.Tasks;
using Orleans;

namespace Turbo.Players.Abstractions;

public interface IPlayerEndpointGrain : IGrainWithIntegerKey
{
    public Task BindConnectionAsync(string connectionId);
}
