using System;

namespace Turbo.Database.Entities.Permissions;

/// <summary>
/// A permission row that is permanent or runs out: an assignment or a membership. A permanent and
/// a temporary row of the same thing may coexist, told apart by <see cref="IsTemporary"/>.
/// </summary>
public interface IPermissionExpiringEntity
{
    /// <summary>UTC. <c>null</c> for a permanent row.</summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>Whether <see cref="ExpiresAt"/> is set. Kept by the writer; part of the unique key.</summary>
    public bool IsTemporary { get; set; }
}
