using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Turbo.Database.Attributes;

namespace Turbo.Database.Entities.Navigator;

[TurboEntity]
[Table("navigator_eventcats")]
public class NavigatorEventCategoryEntity : Entity
{
    [Key]
    [Column("id")]
    public new int Id { get; set; }

    [Column("name")]
    public required string Name { get; set; }

    [Column("visible")]
    public required bool Visible { get; set; }
}
