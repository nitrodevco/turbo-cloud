using System.Collections.Immutable;
using Orleans;

namespace Turbo.Primitives.Players.Snapshots.Permissions;

/// <summary>Every permission group in the hotel, as of one version of the directory.</summary>
[GenerateSerializer, Immutable]
public sealed record PermissionGroupDirectorySnapshot
{
    public static readonly PermissionGroupDirectorySnapshot EMPTY = new()
    {
        Version = 0,
        Groups = ImmutableDictionary<int, PermissionGroupSnapshot>.Empty,
    };

    /// <summary>Bumped on every change, so a holder can tell a newer snapshot from an older one.</summary>
    [Id(0)]
    public required long Version { get; init; }

    [Id(1)]
    public required ImmutableDictionary<int, PermissionGroupSnapshot> Groups { get; init; }
}
