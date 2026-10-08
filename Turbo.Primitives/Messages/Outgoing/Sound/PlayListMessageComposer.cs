using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Sound.Snapshots;

namespace Turbo.Primitives.Messages.Outgoing.Sound;

/// <summary>
/// A sound machine's songs. The client plays the list round on its own, from the point
/// <see cref="SynchronizationCountMs"/> into it (taken modulo the list's total length), so
/// everyone in the room hears the same part.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record PlayListMessageComposer : IComposer
{
    [Id(0)]
    public required int SynchronizationCountMs { get; init; }

    /// <summary>The client reads id, length, name and author of each.</summary>
    [Id(1)]
    public required ImmutableArray<SongSnapshot> Songs { get; init; }
}
