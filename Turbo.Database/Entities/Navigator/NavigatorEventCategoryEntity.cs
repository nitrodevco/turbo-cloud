namespace Turbo.Database.Entities.Navigator;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

[Table("navigator_eventcats")]
public class NavigatorEventCategoryEntity : Entity
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    [Required]
    public string Name { get; set; }

    [Column("visible")]
    [Required]
    public bool Visible { get; set; }
}
