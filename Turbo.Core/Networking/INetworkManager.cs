namespace Turbo.Core.Networking;

using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;

using Turbo.Core.Configuration;

public interface INetworkManager
{
    public void SetupServers(IList<INetworkServerConfig> hostConfigs);

    public Task StartServersAsync();
}
