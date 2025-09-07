using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Turbo.Database.Attributes;

namespace Turbo.Database.Entities.Navigator;

[TurboEntity]
[Table("navigator_flatcats")]
public class NavigatorFlatCategoryEntity : Entity
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

    [Column("min_rank")]
    [DefaultValue(1)]
    public required int MinRank { get; set; }

    [Column("order_num")]
    [DefaultValue(0)]
    public required int OrderNum { get; set; }
}
