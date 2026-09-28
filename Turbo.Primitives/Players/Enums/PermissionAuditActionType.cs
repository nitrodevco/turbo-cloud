namespace Turbo.Primitives.Players.Enums;

/// <summary>What a permission audit row records. Stored as an int: never renumber.</summary>
public enum PermissionAuditActionType
{
    NodeSet = 0,
    NodeUnset = 1,
    MetaSet = 2,
    MetaUnset = 3,

    /// <summary>A player was put in a group.</summary>
    GroupAdded = 4,

    /// <summary>A player was taken out of a group.</summary>
    GroupRemoved = 5,

    ParentAdded = 6,
    ParentRemoved = 7,
    GroupCreated = 8,
    GroupDeleted = 9,
    GroupReweighted = 10,
    GroupRenamed = 11,

    /// <summary>A temporary node, meta value or membership ran out.</summary>
    Expired = 12,
}
