using System;

namespace Turbo.Core.Configuration;

public class IDispatcherOptions
{
    public int GlobalQueueCapacity { get; init; } = 32768;
    public int Workers { get; init; } = Environment.ProcessorCount; // parallelism
    public int MaxPendingPerSession { get; init; } = 512; // soft cap (ordering chain)
    public int RateCapacity { get; init; } = 100; // burst
    public int RateRefillPerSec { get; init; } = 50; // sustained rate
    public int RateViolationsBeforeKick { get; init; } = 3;
    public int PauseReadsThresholdGlobal { get; init; } = 28000; // pause dotnetty reads
    public int ResumeReadsThresholdGlobal { get; init; } = 20000;
}