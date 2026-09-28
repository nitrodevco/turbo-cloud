using System;
using System.Collections.Immutable;
using System.Linq;
using Orleans;

namespace Turbo.Primitives.Players.Snapshots.Permissions;

/// <summary>
/// One player's permissions, resolved: what every check reads. Produced by
/// <see cref="Turbo.Primitives.Players.Permissions.PermissionResolver"/>.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record ResolvedPermissionsSnapshot
{
    /// <summary>Holds nothing: what a player is taken to hold when their permissions cannot be read.</summary>
    public static readonly ResolvedPermissionsSnapshot EMPTY = new()
    {
        Granted = [],
        Meta = ImmutableDictionary<string, string>.Empty,
        UnregisteredNodes = [],
        UnregisteredMetaKeys = [],
    };

    /// <summary>The registered nodes the player holds.</summary>
    [Id(0)]
    public required ImmutableHashSet<string> Granted { get; init; }

    /// <summary>The value of each registered meta key the player or one of their groups sets.</summary>
    [Id(1)]
    public required ImmutableDictionary<string, string> Meta { get; init; }

    /// <summary>
    /// UTC. When the earliest assignment that took part in this resolution runs out, and the
    /// result has to be worked out again. <c>null</c> when nothing expires.
    /// </summary>
    [Id(2)]
    public DateTime? NextExpiresAt { get; init; }

    /// <summary>Concrete nodes assigned to the player or their groups that nothing registered.</summary>
    [Id(3)]
    public required ImmutableArray<string> UnregisteredNodes { get; init; }

    /// <summary>Meta keys set on the player or their groups that nothing registered.</summary>
    [Id(4)]
    public required ImmutableArray<string> UnregisteredMetaKeys { get; init; }

    public bool Has(string node) => Granted.Contains(node);

    /// <summary>
    /// Whether the player holds the same nodes and meta values in both; record equality compares
    /// the collections by reference. Expiry and unregistered assignments are not what they hold.
    /// </summary>
    public bool HoldsSame(ResolvedPermissionsSnapshot other) =>
        Granted.SetEquals(other.Granted)
        && Meta.Count == other.Meta.Count
        && Meta.All(x =>
            other.Meta.TryGetValue(x.Key, out var value)
            && string.Equals(value, x.Value, StringComparison.Ordinal)
        );
}
