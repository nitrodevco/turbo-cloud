using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Sound.Snapshots;

namespace Turbo.Primitives.Messages.Outgoing.Sound;

/// <summary>Songs the client asked about, track included, so it can show and play them.</summary>
[GenerateSerializer, Immutable]
public sealed record TraxSongInfoMessageComposer : IComposer
{
    [Id(0)]
    public required ImmutableArray<SongSnapshot> Songs { get; init; }
}
