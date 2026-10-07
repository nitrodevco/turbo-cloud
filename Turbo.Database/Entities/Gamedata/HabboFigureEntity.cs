using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Turbo.Primitives.Gamedata.Enums;

namespace Turbo.Database.Entities.Gamedata;

/// <summary>
/// One record of Habbo's figure data as it was last taken in. The next import compares against it
/// to tell what Habbo changed from what the hotel changed.
/// </summary>
[Table("habbo_figure_records")]
[Index(nameof(Kind), nameof(Key), IsUnique = true)]
public class HabboFigureEntity : TurboEntity
{
    [Column("kind")]
    public required FigureRecordKind Kind { get; set; }

    [Column("record_key")]
    [MaxLength(GamedataFigureEntity.KEY_MAX_LENGTH)]
    public required string Key { get; set; }

    [Column("data", TypeName = "longtext")]
    public required string Data { get; set; }

    /// <summary>The version it was last taken in from.</summary>
    [Column("version_id")]
    public required int HabboFigureVersionEntityId { get; set; }
}
