using System.Threading.Tasks;

namespace Turbo.Networking.Abstractions;

public interface INetworkManager
{
    public Task StartAsync();
    public Task StopAsync();
}
