using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Networking.Abstractions;

public interface INetworkManager
{
    public Task StartAsync(CancellationToken ct);
    public Task StopAsync();
}
