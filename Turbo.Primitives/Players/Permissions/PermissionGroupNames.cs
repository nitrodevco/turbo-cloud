namespace Turbo.Primitives.Players.Permissions;

/// <summary>Group names the server itself depends on, and the shape a group name takes.</summary>
public static class PermissionGroupNames
{
    /// <summary>Held implicitly by every player, whatever their memberships say.</summary>
    public const string DEFAULT = "default";

    /// <summary>Longest group name. Fixed by the width of <c>permission_groups.name</c>.</summary>
    public const int MAX_LENGTH = 64;

    /// <summary>Longest display name. Fixed by the width of <c>permission_groups.display_name</c>.</summary>
    public const int DISPLAY_NAME_MAX_LENGTH = 128;

    /// <summary>A group name is one node segment: lowercase letters, digits and underscores.</summary>
    public static bool IsValid(string? name) =>
        name is not null && name.Length <= MAX_LENGTH && PermissionNodeFormat.IsValidSegment(name);

    public static bool IsValidDisplayName(string? displayName) =>
        !string.IsNullOrWhiteSpace(displayName) && displayName.Length <= DISPLAY_NAME_MAX_LENGTH;
}
