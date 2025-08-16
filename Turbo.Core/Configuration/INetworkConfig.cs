namespace Turbo.Core.Configuration;

using System.Collections;
using System.Collections.Generic;

public class INetworkConfig
{
    public int BossEventLoopThreads { get; init; }

    public int WorkerEventLoopThreads { get; init; }

    public List<INetworkServerConfig> Servers { get; init; }

    public IDispatcherOptions DispatcherOptions { get; init; }
}
