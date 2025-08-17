using System.Collections;
using System.Collections.Generic;

namespace Turbo.Core.Configuration;

public class INetworkConfig
{
    public int BossEventLoopThreads { get; init; }

    public int WorkerEventLoopThreads { get; init; }
    public int RateLimitCapacity { get; init; }
    public double RateLimitRefillPerSecond { get; init; }
    public int RateLimitStripes { get; init; }

    public List<INetworkServerConfig> Servers { get; init; }

    public IDispatcherOptions DispatcherOptions { get; init; }
}
