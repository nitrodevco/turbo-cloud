using System.Threading.Tasks;

namespace Turbo.Core.Networking.Servers;

public interface IServer
{
    public string Host { get; }

    public int Port { get; }

    public Task StartAsync();

    public Task StopAsync();
}
