using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Sound.Snapshots;

namespace Turbo.Primitives.Messages.Outgoing.Sound;

/// <summary>The song disks in a player's inventory: what the playlist editor offers to add.</summary>
[GenerateSerializer, Immutable]
public sealed record UserSongDisksInventoryMessageComposer : IComposer
{
    [Id(0)]
    public required ImmutableArray<SongDiskSnapshot> Disks { get; init; }
}
