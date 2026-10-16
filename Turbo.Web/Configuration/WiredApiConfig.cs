namespace Turbo.Web.Configuration;

/// <summary>
/// The Wired Variables endpoints of the public API (<c>Turbo:Web:WiredApi</c>), which a room opens
/// with a "Variable Global Add-on: Web API". Every room has its own limits per kind of call, as
/// Habbo's (The Wired Faculty, #api-technical-info, 2026-09-01): so many a minute, and at most a
/// burst of so many in any 10 seconds.
/// </summary>
public sealed class WiredApiConfig
{
    public bool Enabled { get; init; } = true;

    /// <summary>Longest request body read, in bytes.</summary>
    public int MaxBodyBytes { get; init; } = 64 * 1024;

    public WiredApiLimit SimpleReads { get; init; } = new() { PerMinute = 300, Burst = 60 };

    public WiredApiLimit Lists { get; init; } = new() { PerMinute = 120, Burst = 20 };

    public WiredApiLimit ProfileReads { get; init; } = new() { PerMinute = 120, Burst = 20 };

    public WiredApiLimit Writes { get; init; } = new() { PerMinute = 120, Burst = 30 };

    public WiredApiLimit BulkDeletes { get; init; } = new() { PerMinute = 10, Burst = 5 };

    /// <summary>Batches and profile PATCHes (Habbo counts a profile PATCH as a batch); its burst is not published.</summary>
    public WiredApiLimit Batches { get; init; } = new() { PerMinute = 30, Burst = 10 };
}

public sealed class WiredApiLimit
{
    public int PerMinute { get; init; }

    public int Burst { get; init; }
}
