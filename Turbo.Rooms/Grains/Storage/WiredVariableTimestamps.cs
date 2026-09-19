namespace Turbo.Rooms.Grains.Storage;

/// <summary>When a variable value was created and last written, in unix milliseconds.</summary>
public sealed record WiredVariableTimestamps(long CreatedAtMs, long UpdatedAtMs);
