using Turbo.Primitives.Players.Enums;

namespace Turbo.Primitives.Players.Permissions;

/// <summary>A meta key a group or player may carry a value for.</summary>
/// <param name="Key">The key, in the same dotted form as a node.</param>
/// <param name="Description">What the value means, for the console and the check trace.</param>
/// <param name="Selection">
/// How the value is chosen when several sources set it. Limits a group raises take the highest.
/// </param>
public sealed record PermissionMetaDefinition(
    string Key,
    string Description,
    PermissionMetaSelectionType Selection = PermissionMetaSelectionType.Inheritance
);
