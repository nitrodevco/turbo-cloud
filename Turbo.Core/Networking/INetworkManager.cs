using System;
using System.Threading.Tasks;

namespace Turbo.Core.Networking;

public interface INetworkManager
{
    public Task StartAsync();
    public Task StopAsync();
}
