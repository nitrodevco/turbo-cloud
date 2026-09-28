namespace Turbo.Primitives.Players.Enums;

/// <summary>What a write to a group's or a player's permissions came to.</summary>
public enum PermissionChangeResultType
{
    /// <summary>Saved, audited, and every affected player re-resolved.</summary>
    Changed = 0,

    /// <summary>The state already was what was asked for; nothing was written or audited.</summary>
    Unchanged = 1,

    /// <summary>No group has that name.</summary>
    UnknownGroup = 2,

    /// <summary>A malformed node, meta key or group name.</summary>
    Invalid = 3,

    /// <summary>An expiry that has already passed.</summary>
    Expired = 4,

    /// <summary>The default group cannot be deleted, joined or left.</summary>
    ProtectedGroup = 5,

    /// <summary>The parent would make the group inherit from itself.</summary>
    WouldCycle = 6,

    /// <summary>A group with that name already exists.</summary>
    AlreadyExists = 7,

    /// <summary>There was nothing of that name to remove.</summary>
    NotFound = 8,
}
