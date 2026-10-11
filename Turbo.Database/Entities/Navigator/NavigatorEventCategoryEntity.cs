using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Turbo.Database.Entities.Navigator;

[Comment("The categories a room event can be listed under in the navigator.")]
[Table("navigator_event_categories")]
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
