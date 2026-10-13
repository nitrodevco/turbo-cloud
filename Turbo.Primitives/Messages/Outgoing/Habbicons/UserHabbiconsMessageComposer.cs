using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Habbicons.Snapshots;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Habbicons;

/// <summary>The Habbicons the player has and the ones they used last, newest first; sent at login.</summary>
[GenerateSerializer, Immutable]
public sealed record UserHabbiconsMessageComposer : IComposer
{
    [Id(0)]
    public required ImmutableArray<OwnedHabbiconSnapshot> Habbicons { get; init; }

    [Id(1)]
    public required ImmutableArray<int> RecentHabbiconIds { get; init; }
}
