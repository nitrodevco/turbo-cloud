using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Turbo.Core.Configuration;

namespace Turbo.Core.Networking;

public interface INetworkManager
{
    public void SetupServers(IList<INetworkHostConfig> hostConfigs);
    public Task StartServersAsync();
}