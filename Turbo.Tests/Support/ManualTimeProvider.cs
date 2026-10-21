using System;

namespace Turbo.Tests.Support;

/// <summary>
/// A clock a test moves by hand, so a window boundary can be crossed exactly. It keeps both of a
/// <see cref="TimeProvider"/>'s clocks: the time of day (<see cref="GetUtcNow"/>) and the
/// monotonic timestamp (<see cref="GetTimestamp"/>) a room's <c>NowMs</c> reads.
/// <see cref="Advance"/> moves both; <see cref="Set"/> moves only the time of day, as changing a
/// machine's date leaves its monotonic clock where it was.
/// </summary>
public sealed class ManualTimeProvider(DateTimeOffset? start = null) : TimeProvider
{
    /// <summary>Where a room harness's clock starts, so a room test reads the same time on every run.</summary>
    public static readonly DateTimeOffset FixedStart = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private DateTimeOffset _now = start ?? DateTimeOffset.UtcNow;

    // In TimeSpan ticks (TimestampFrequency). It starts an hour in, as a machine's uptime has,
    // so room code that reads 0 as "never" does not mistake the start for it.
    private long _timestamp = TimeSpan.FromHours(1).Ticks;

    public override DateTimeOffset GetUtcNow() => _now;

    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    public override long GetTimestamp() => _timestamp;

    public void Set(DateTime utc) => _now = new DateTimeOffset(utc, TimeSpan.Zero);

    public void Advance(TimeSpan by)
    {
        if (by < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(by), by, "Time only moves forward.");

        _now += by;
        _timestamp += by.Ticks;
    }

    /// <summary><see cref="Advance"/> by whole milliseconds, the unit room time is kept in.</summary>
    public void AdvanceMs(long ms) => Advance(TimeSpan.FromMilliseconds(ms));
}
