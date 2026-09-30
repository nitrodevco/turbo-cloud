namespace Turbo.Database.Entities.Permissions;

/// <summary>A node or meta value set on a group or a player: an expiring row that carries a value.</summary>
public interface IPermissionAssignmentEntity<TValue> : IPermissionExpiringEntity
{
    public TValue Value { get; set; }
}
