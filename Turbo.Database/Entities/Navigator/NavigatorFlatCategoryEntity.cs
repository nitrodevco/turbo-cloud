using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.Database.Entities.Navigator;

[Table("navigator_flatcats")]
public class NavigatorFlatCategoryEntity : TurboEntity
{
    [Key]
    [Column("id")]
    public new int Id { get; set; }

    [Column("name")]
    public required string Name { get; set; }

    [Column("visible")]
    [DefaultValue(true)]
    public required bool Visible { get; set; }

    [Column("automatic")]
    [DefaultValue(true)] // TODO hmm
    public required bool Automatic { get; set; }

    [Column("automatic_category")]
    public string? AutomaticCategory { get; set; }

    [Column("global_category")]
    public string? GlobalCategory { get; set; }

    [Column("staff_only")]
    [DefaultValue(false)]
    public required bool StaffOnly { get; set; }

    /// <summary>
    /// The lowest rank that sees the category, as retro emulators and their CMS panels store it.
    /// Compared against the player's security level, with a regular player (level 0) counting as
    /// rank 1, so the retro default of 1 still means everyone. See <c>docs/permissions.md</c> §16.
    /// </summary>
    [Column("min_rank")]
    [DefaultValue(1)]
    public required int MinRank { get; set; }

    /// <summary>
    /// A permission node the player must also hold to see the category (a VIP-only category,
    /// say), or null for none. What <see cref="MinRank"/> cannot say.
    /// </summary>
    [Column("required_node")]
    [MaxLength(PermissionNodeFormat.MAX_LENGTH)]
    public string? RequiredNode { get; set; }

    [Column("order_num")]
    [DefaultValue(0)]
    public required int OrderNum { get; set; }
}
