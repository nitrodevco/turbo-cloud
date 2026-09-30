using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.Database.Entities.Permissions;

/// <summary>
/// A named set of permission nodes and meta that players hold and other groups inherit. See
/// <c>docs/permissions.md</c> §5.
/// </summary>
[Table("permission_groups")]
[Index(nameof(Name), IsUnique = true)]
public class PermissionGroupEntity : TurboEntity
{
    /// <summary>The stable lowercase key (<c>moderator</c>); what the console and the seed name it by.</summary>
    [Column("name")]
    [MaxLength(PermissionGroupNames.MAX_LENGTH)]
    public required string Name { get; set; }

    [Column("display_name")]
    [MaxLength(PermissionGroupNames.DISPLAY_NAME_MAX_LENGTH)]
    public required string DisplayName { get; set; }

    /// <summary>Higher wins when two groups a player holds disagree.</summary>
    [Column("weight")]
    [DefaultValue(0)]
    public required int Weight { get; set; }

    [InverseProperty(nameof(PermissionGroupParentEntity.GroupEntity))]
    public List<PermissionGroupParentEntity>? Parents { get; set; }

    public List<PermissionGroupNodeEntity>? Nodes { get; set; }

    public List<PermissionGroupMetaEntity>? Meta { get; set; }
}
