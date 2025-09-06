using System;

namespace Turbo.Networking.Configuration;

public class NetworkIncomingQueueConfig
{
    public int MaxQueue { get; init; }
    public int MaxBatch { get; init; }
    public TimeSpan MaxLatency { get; init; }
    public int HardLimit { get; init; }
}
