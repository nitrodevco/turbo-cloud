using System;
using System.Linq;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Snapshots.Permissions;

namespace Turbo.Primitives.Players.Permissions;

/// <summary>
/// The rule for previewing a group (<c>docs/permissions.md</c>, §9, <i>Previewing a group</i>):
/// a staff member may take on what a plain member of a group holds when the group is lighter than
/// their own heaviest group and gives them no node they do not hold already. So a preview only
/// ever takes powers away, and the editor's weight checks, which read the previewing player's
/// groups, can never come out heavier than the player really is. A holder of
/// <c>permissions.manage</c> and <c>permissions.superuser</c>, who may give themselves anything,
/// is bound by neither. The permission grain asks it when a preview starts and again at every
/// resolve, so a preview the rule would no longer allow ends.
/// </summary>
public static class PermissionPreview
{
    /// <summary>What a plain member of <paramref name="group"/> holds: the group, its parents and default, nothing of their own.</summary>
    public static ResolvedPermissionsSnapshot MemberOf(
        PermissionRegistry registry,
        PermissionGroupDirectorySnapshot groups,
        PermissionGroupSnapshot group,
        DateTime now
    ) =>
        PermissionResolver.Resolve(
            registry,
            groups.Groups,
            PlayerPermissionAssignmentsSnapshot.MemberOf(group.Id),
            now
        );

    /// <summary>
    /// Why a player holding <paramref name="standing"/> may not preview <paramref name="group"/>,
    /// which resolves to <paramref name="previewed"/>; <c>null</c> when they may. Membership nodes
    /// (<c>group.&lt;name&gt;</c>) are not asked about: holding the previewed group's is the point.
    /// </summary>
    public static PermissionPreviewResultSnapshot? Refusal(
        PermissionGroupDirectorySnapshot groups,
        ResolvedPermissionsSnapshot standing,
        PermissionGroupSnapshot group,
        ResolvedPermissionsSnapshot previewed
    )
    {
        if (
            standing.Has(PermissionNodes.Permissions.MANAGE)
            && standing.Has(PermissionNodes.Permissions.SUPERUSER)
        )
            return null;

        if (group.Weight >= PermissionEditor.HeaviestOf(groups, standing))
            return new() { Result = PermissionPreviewResultType.GroupTooHeavy };

        var missing = previewed
            .Granted.Where(node =>
                !PermissionGroupNames.IsGroupNode(node) && !standing.Granted.Contains(node)
            )
            .Order(StringComparer.Ordinal)
            .FirstOrDefault();

        return missing is null
            ? null
            : new() { Result = PermissionPreviewResultType.NodeNotHeld, Node = missing };
    }
}
