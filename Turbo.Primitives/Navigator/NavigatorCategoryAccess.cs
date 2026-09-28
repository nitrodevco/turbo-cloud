using System;
using Turbo.Primitives.Navigator.Snapshots;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Players.Snapshots.Permissions;

namespace Turbo.Primitives.Navigator;

/// <summary>
/// Who may see a flat category, and so list a room in it. See <c>docs/permissions.md</c> §16.
/// </summary>
public static class NavigatorCategoryAccess
{
    /// <summary>
    /// Visible, and every one of: a staff-only category needs <c>navigator.category.staff</c> (the
    /// client hides it below security level 7, which that node projects to); <c>MinRank</c> is at
    /// most the player's rank; the category's required node, if it names one, is held.
    /// </summary>
    public static bool CanSee(
        NavigatorFlatCategorySnapshot category,
        ResolvedPermissionsSnapshot permissions,
        SecurityLevelType securityLevel
    ) =>
        category.Visible
        && (!category.StaffOnly || permissions.Has(PermissionNodes.Navigator.CATEGORY_STAFF))
        && category.MinRank <= RankOf(securityLevel)
        && (category.RequiredNode is null || permissions.Has(category.RequiredNode));

    /// <summary>
    /// The retro rank a security level stands for: a regular player (level 0) is rank 1, as in the
    /// retro emulators whose <c>min_rank</c> this reads, so a category left at the default of 1 is
    /// everyone's.
    /// </summary>
    public static int RankOf(SecurityLevelType securityLevel) => Math.Max(1, (int)securityLevel);
}
