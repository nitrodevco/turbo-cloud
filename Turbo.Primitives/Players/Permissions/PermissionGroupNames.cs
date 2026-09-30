using System;

namespace Turbo.Primitives.Players.Permissions;

/// <summary>Group names the server itself depends on, and the shape a group name takes.</summary>
public static class PermissionGroupNames
{
    /// <summary>Held implicitly by every player, whatever their memberships say.</summary>
    public const string DEFAULT = "default";

    /// <summary>
    /// The root of the membership nodes: a player holds <c>group.&lt;name&gt;</c> for every group
    /// they reach, directly or by inheritance, default included. Reserved: nothing registers a
    /// node under it and nothing assigns one; the resolver alone grants them.
    /// </summary>
    public const string NODE_ROOT = "group";

    private const string NODE_PREFIX = NODE_ROOT + ".";

    /// <summary>Longest group name. Fixed by the width of <c>permission_groups.name</c>.</summary>
    public const int MAX_LENGTH = 64;

    /// <summary>Longest display name. Fixed by the width of <c>permission_groups.display_name</c>.</summary>
    public const int DISPLAY_NAME_MAX_LENGTH = 128;

    /// <summary>A group name is one node segment: lowercase letters, digits and underscores.</summary>
    public static bool IsValid(string? name) =>
        name is not null && name.Length <= MAX_LENGTH && PermissionNodeFormat.IsValidSegment(name);

    public static bool IsValidDisplayName(string? displayName) =>
        !string.IsNullOrWhiteSpace(displayName) && displayName.Length <= DISPLAY_NAME_MAX_LENGTH;

    /// <summary>The membership node for a group: <c>group.vip</c> for <c>vip</c>.</summary>
    public static string ToNode(string name) => NODE_PREFIX + name;

    /// <summary>
    /// Whether <paramref name="node"/> is a membership node, or an assignment (a wildcard like
    /// <c>group.*</c>) under the reserved root.
    /// </summary>
    public static bool IsGroupNode(string node) =>
        string.Equals(PermissionNodeFormat.RootOf(node), NODE_ROOT, StringComparison.Ordinal);
}
