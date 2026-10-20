using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Tracking;

/// <summary>The latency test's report, after latencytest.report.index pings (LatencyTracker.as).</summary>
public record LatencyPingReportMessage : IClientTimerMessage
{
    /// <summary>The average round trip of the pings, in milliseconds.</summary>
    public int AverageLatency { get; init; }

    /// <summary>The average of the pings under twice that average, in milliseconds.</summary>
    public int ValidPingAverage { get; init; }
    public int NumPings { get; init; }
}
