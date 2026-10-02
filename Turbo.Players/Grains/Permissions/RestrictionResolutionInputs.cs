using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Players.Snapshots.Permissions;

namespace Turbo.Players.Grains.Permissions;

/// <summary>Immutable inputs retained to distinguish expiry from an explicit permission change.</summary>
internal sealed record RestrictionResolutionInputs(
    PermissionRegistry Registry,
    PermissionGroupDirectorySnapshot Groups,
    PlayerPermissionAssignmentsSnapshot Assignments,
    ResolvedPermissionsSnapshot Resolved
);
