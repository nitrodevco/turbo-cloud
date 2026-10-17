namespace Turbo.Players.Configuration;

/// <summary>Permission tunables, read by the permission grains. See <c>docs/permissions.md</c>.</summary>
public class PermissionConfig
{
    /// <summary>Most audit rows one request for a player's or a group's history returns.</summary>
    public int AuditPageLimit { get; init; } = 100;

    /// <summary>Most rows one console lookup (group members, who holds a node) returns.</summary>
    public int LookupPageLimit { get; init; } = 200;

    /// <summary>
    /// Longest the grains wait before looking for expired assignments again. An expiry further
    /// out than this is reached in steps; the cap keeps the wait inside what a grain timer takes.
    /// </summary>
    public int ExpiryCheckMaxMs { get; init; } = 86_400_000;

    /// <summary>How long to wait before trying again after expiring assignments failed.</summary>
    public int ExpiryRetryMs { get; init; } = 60_000;

    /// <summary>
    /// How often a player's permission grain asks whether its player is still online. Each check
    /// that finds them online pins it for two of these (what their client was told lives only in
    /// that activation); from the first that finds no session it is left to idle collection.
    /// </summary>
    public int OnlineCheckMs { get; init; } = 60_000;
}
