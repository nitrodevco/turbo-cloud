namespace Turbo.Players.Configuration;

/// <summary>Permission tunables, read by the permission grains. See <c>docs/permissions.md</c>.</summary>
public class PermissionConfig
{
    /// <summary>Most audit rows one request for a player's or a group's history returns.</summary>
    public int AuditPageLimit { get; init; } = 100;

    /// <summary>
    /// Longest the grains wait before looking for expired assignments again. An expiry further
    /// out than this is reached in steps; the cap keeps the wait inside what a grain timer takes.
    /// </summary>
    public int ExpiryCheckMaxMs { get; init; } = 86_400_000;

    /// <summary>How long to wait before trying again after expiring assignments failed.</summary>
    public int ExpiryRetryMs { get; init; } = 60_000;
}
