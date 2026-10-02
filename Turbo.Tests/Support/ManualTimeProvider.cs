using System;

namespace Turbo.Tests.Support;

/// <summary>A clock a test moves by hand, so a window boundary can be crossed exactly.</summary>
public sealed class ManualTimeProvider(DateTimeOffset? start = null) : TimeProvider
{
    private DateTimeOffset _now = start ?? DateTimeOffset.UtcNow;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Set(DateTime utc) => _now = new DateTimeOffset(utc, TimeSpan.Zero);

    public void Advance(TimeSpan by) => _now += by;
}
