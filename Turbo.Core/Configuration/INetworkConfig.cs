using System.Collections;
using System.Collections.Generic;

namespace Turbo.Core.Configuration;

public class INetworkConfig
{
    public INetworkServerConfig TcpServer { get; init; }
}
