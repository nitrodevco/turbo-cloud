using System;
using Orleans;

namespace Turbo.Primitives.Gamedata.Snapshots;

/// <summary>
/// One of Habbo's environments that serve its HTML client (<c>production</c>, <c>sandbox</c>): what
/// its <c>/gamedata/clienturls</c> said when last asked, and the release the hotel took from it.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record HabboClientEnvironmentSnapshot
{
    [Id(0)]
    public required string Environment { get; init; }

    [Id(1)]
    public required string Host { get; init; }

    /// <summary>When it was last asked; null before the first time.</summary>
    [Id(2)]
    public DateTime? CheckedAt { get; init; }

    /// <summary>The release it serves now (<c>html-windows-version</c>); null when it serves none.</summary>
    [Id(3)]
    public string? LiveVersion { get; init; }

    /// <summary>Why the last check failed; null when it answered.</summary>
    [Id(4)]
    public string? Error { get; init; }

    /// <summary>The release the hotel downloaded from it; null when none yet.</summary>
    [Id(5)]
    public string? Version { get; init; }

    /// <summary>Whether its live release is downloading now.</summary>
    [Id(6)]
    public required bool Downloading { get; init; }

    /// <summary>Why its live release could not be downloaded, the last time it was tried.</summary>
    [Id(7)]
    public string? DownloadError { get; init; }
}
