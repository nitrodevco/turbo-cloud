using Orleans;
using Turbo.Primitives.Players.Enums;

namespace Turbo.Primitives.Players.Snapshots.Permissions;

/// <summary>
/// A group or a player with an assignment that names a node, exactly or by wildcard: one row of
/// <c>perm search &lt;node&gt;</c>.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record PermissionNodeHolderSnapshot
{
    [Id(0)]
    public required PermissionAuditTargetType TargetType { get; init; }

    /// <summary>The group id or the player id, by <see cref="TargetType"/>.</summary>
    [Id(1)]
    public required int TargetId { get; init; }

    [Id(2)]
    public required PermissionNodeAssignmentSnapshot Assignment { get; init; }
}
