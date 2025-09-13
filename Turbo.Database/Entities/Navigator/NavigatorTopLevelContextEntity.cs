using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Turbo.Database.Entities.Navigator;

[Table("navigator_top_level_contexts")]
[Index(nameof(SearchCode), IsUnique = true)]
public class NavigatorTopLevelContextEntity : TurboEntity
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
