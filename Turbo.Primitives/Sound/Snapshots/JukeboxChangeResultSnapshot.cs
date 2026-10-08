using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Sound.Enums;

namespace Turbo.Primitives.Sound.Snapshots;

/// <summary>A change to a jukebox's playlist: how it went, and the playlist after it.</summary>
[GenerateSerializer, Immutable]
public sealed record JukeboxChangeResultSnapshot
{
    [Id(0)]
    public required JukeboxChangeResultType Result { get; init; }

    /// <summary>The disks in the jukebox, in playing order, whether or not anything changed.</summary>
    [Id(1)]
    public required ImmutableArray<SongDiskSnapshot> Disks { get; init; }
}
