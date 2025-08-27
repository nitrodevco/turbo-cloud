using System;

namespace Turbo.Core.Configuration;

public class INetworkIncomingQueueConfig
{
    public int MaxQueue { get; init; }
    public int MaxBatch { get; init; }
    public TimeSpan MaxLatency { get; init; }
    public int HardLimit { get; init; }
}
