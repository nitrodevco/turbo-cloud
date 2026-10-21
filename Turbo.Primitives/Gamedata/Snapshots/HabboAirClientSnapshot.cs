using System;
using Orleans;

namespace Turbo.Primitives.Gamedata.Snapshots;

/// <summary>
/// Habbo's AIR client as the hotel has it: the newest release the environments named when last
/// asked, and the one kept for the window templates - when Habbo built it, when it was downloaded,
/// and its <c>HabboAir.swf</c>'s size.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record HabboAirClientSnapshot
{
    /// <summary>The newest release named (<c>flash-windows-version</c>); null when none was.</summary>
    [Id(0)]
    public string? Version { get; init; }

    /// <summary>The release kept; null until a build first asks for it.</summary>
    [Id(1)]
    public string? Kept { get; init; }

    /// <summary>When Habbo built the one kept, as its zip dates <c>HabboAir.swf</c>.</summary>
    [Id(2)]
    public DateTime? BuiltAt { get; init; }

    [Id(3)]
    public DateTime? DownloadedAt { get; init; }

    /// <summary>The kept <c>HabboAir.swf</c>'s size in bytes; 0 when none is kept.</summary>
    [Id(4)]
    public long Size { get; init; }
}
