using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Tracking;

public record PerformanceLogMessage : IMessageEvent
{
    public int ElapsedTime { get; init; }
    public string? UserAgent { get; init; }
    public string? FlashVersion { get; init; }
    public string? OS { get; init; }
    public string? Browser { get; init; }
    public bool IsDebugger { get; init; }
    public int MemoryUsage { get; init; }
    public int unknownField { get; init; }
    public int GarbageCollections { get; init; }
    public int AverageFrameRate { get; init; }
    public int SlowUpdates { get; init; }
}
