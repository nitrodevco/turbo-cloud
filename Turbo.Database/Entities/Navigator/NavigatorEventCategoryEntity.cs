using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Turbo.Database.Entities.Navigator;

[Table("navigator_eventcats")]
public class NavigatorEventCategoryEntity : TurboEntity
{
    [Key]
    [Column("id")]
    public new int Id { get; set; }

    [Column("name")]
    public required string Name { get; set; }

    [Column("visible")]
    public required bool Visible { get; set; }
}
