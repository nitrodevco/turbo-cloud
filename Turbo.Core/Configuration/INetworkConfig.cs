using System.Collections;
using System.Collections.Generic;

namespace Turbo.Core.Configuration;

public class INetworkConfig
{
    public int BossEventLoopThreads { get; init; }
    public int WorkerEventLoopThreads { get; init; }
    public List<INetworkServerConfig> Servers { get; init; }
}