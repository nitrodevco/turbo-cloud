namespace Turbo.Primitives.Players.Enums;

/// <summary>What setting a temporary assignment does to one that is already running.</summary>
public enum PermissionExpiryModeType
{
    /// <summary>The new expiry replaces the old one.</summary>
    Replace = 0,

    /// <summary>
    /// The new duration is added to what the running assignment has left, as when a month of
    /// VIP is bought on top of another. With nothing running, the same as replace.
    /// </summary>
    Extend = 1,
}
