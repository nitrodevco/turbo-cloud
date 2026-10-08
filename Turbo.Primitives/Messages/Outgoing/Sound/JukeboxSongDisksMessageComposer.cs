using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Sound.Snapshots;

namespace Turbo.Primitives.Messages.Outgoing.Sound;

/// <summary>
/// The room's jukebox playlist: the disks in playing order, which the client's playlist editor
/// lists and its now-playing positions index.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record JukeboxSongDisksMessageComposer : IComposer
{
    /// <summary>How many disks the jukebox takes.</summary>
    [Id(0)]
    public required int MaxLength { get; init; }

    [Id(1)]
    public required ImmutableArray<SongDiskSnapshot> Disks { get; init; }
}
