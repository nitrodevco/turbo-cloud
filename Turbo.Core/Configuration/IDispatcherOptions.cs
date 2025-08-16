namespace Turbo.Core.Configuration;

public class IDispatcherOptions
{
    public int GlobalQueueCapacity { get; init; }
    public int Workers { get; init; }
    public int MaxPendingPerSession { get; init; }
    public int RateCapacity { get; init; }
    public int RateRefillPerSec { get; init; }
    public int RateViolationsBeforeKick { get; init; }
    public int PauseReadsThresholdGlobal { get; init; }
    public int ResumeReadsThresholdGlobal { get; init; }
}