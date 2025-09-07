using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Attributes;

namespace Turbo.Database.Entities.Navigator;

[TurboEntity]
[Table("navigator_top_level_contexts")]
[Index(nameof(SearchCode), IsUnique = true)]
public class NavigatorTopLevelContextEntity : Entity
{
    [Column("search_code")]
    public required string SearchCode { get; set; }

    [Column("visible")]
    [DefaultValue(true)]
    public required bool Visible { get; set; }

    [Column("order_num")]
    [DefaultValue(0)]
    public required int OrderNum { get; set; }
}
