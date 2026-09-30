using System;
using Turbo.Primitives.Players.Enums;

namespace Turbo.Primitives.Players.Permissions;

/// <summary>What expiry a temporary assignment ends up with when it is set again.</summary>
public static class PermissionExpiry
{
    /// <summary>
    /// The expiry to store. A permanent assignment (<paramref name="requested"/> null) stays
    /// permanent. Extending a temporary one that is still running adds the requested duration
    /// (<paramref name="requested"/> minus <paramref name="now"/>) to what it has left; anything
    /// else takes the requested expiry as it is.
    /// </summary>
    public static DateTime? Resolve(
        DateTime? requested,
        DateTime? existing,
        PermissionExpiryModeType mode,
        DateTime now
    )
    {
        if (
            requested is not { } until
            || mode != PermissionExpiryModeType.Extend
            || existing is not { } running
            || running <= now
        )
            return requested;

        return running + (until - now);
    }
}
