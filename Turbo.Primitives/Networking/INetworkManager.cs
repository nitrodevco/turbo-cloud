using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Primitives.Networking;

public interface INetworkManager
{
    public Task StartAsync(CancellationToken ct);
    public Task StopAsync();
}
