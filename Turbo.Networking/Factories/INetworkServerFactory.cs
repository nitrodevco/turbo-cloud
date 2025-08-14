using Turbo.Core.Configuration;
using Turbo.Core.Networking;

namespace Turbo.Networking.Factories;

public interface INetworkServerFactory
{
    public IServer CreateServerFromConfig(INetworkHostConfig config);
}