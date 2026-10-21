using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Tracking;

/// <summary>
/// The client's performance report, every <c>performancetest.interval</c> seconds up to
/// <c>performancetest.reportlimit</c> times (<c>PerformanceTracker.as</c> sendReport).
/// </summary>
public record PerformanceLogMessage : IMessageEvent
{
    /// <summary>Seconds since the client started.</summary>
    public int ElapsedTime { get; init; }
    public string? UserAgent { get; init; }
    public string? FlashVersion { get; init; }
    public string? OS { get; init; }
    public string? Browser { get; init; }
    public bool IsDebugger { get; init; }

    /// <summary>The memory the client holds, in KB.</summary>
    public int MemoryUsage { get; init; }

    /// <summary>Always -1 from Habbo's client.</summary>
    public int Reserved { get; init; }
    public int GarbageCollections { get; init; }

    /// <summary>
    /// The average time between frames since the last report, in milliseconds, leaving out the
    /// slow ones; 1000 over it is the frame rate.
    /// </summary>
    public int AverageUpdateInterval { get; init; }

    /// <summary>The frames since the last report slower than <c>performancetest.slowupdatelimit</c>.</summary>
    public int SlowUpdates { get; init; }
}
