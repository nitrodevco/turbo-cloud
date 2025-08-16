namespace Turbo.Core.Networking.Servers;

using System.Threading.Tasks;

public interface IServer
{
    public string Host { get; }

    public int Port { get; }

    public Task StartAsync();

    public Task StopAsync();
}
