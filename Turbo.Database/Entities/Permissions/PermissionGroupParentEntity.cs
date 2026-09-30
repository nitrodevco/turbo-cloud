using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Turbo.Database.Entities.Permissions;

/// <summary>One group inheriting from another. Cycles are refused on write and ignored on resolve.</summary>
[Table("permission_group_parents")]
[Index(nameof(GroupEntityId), nameof(ParentGroupEntityId), IsUnique = true)]
// A group edit re-resolves everything that inherits from it, which reads by parent.
[Index(nameof(ParentGroupEntityId))]
public class PermissionGroupParentEntity : TurboEntity
{
    [Column("group_id")]
    public required int GroupEntityId { get; set; }

    [Column("parent_group_id")]
    public required int ParentGroupEntityId { get; set; }

    [ForeignKey(nameof(GroupEntityId))]
    public PermissionGroupEntity? GroupEntity { get; set; }

    [ForeignKey(nameof(ParentGroupEntityId))]
    public PermissionGroupEntity? ParentGroupEntity { get; set; }
}
