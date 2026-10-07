using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Turbo.Primitives.Gamedata.Enums;

namespace Turbo.Database.Entities.Gamedata;

/// <summary>
/// One record of the hotel's figure data - a colour, a kind of clothing or a piece of clothing -
/// its fields as JSON, by the names Habbo's file gives them. Habbo's taken in, and the hotel's
/// own. What the client draws an avatar from, and what a figure is checked against.
/// </summary>
[Table("gamedata_figure_records")]
[Index(nameof(Kind), nameof(Key), IsUnique = true)]
[Index(nameof(Kind), nameof(Group))]
public class GamedataFigureEntity : TurboEntity
{
    public const int KEY_MAX_LENGTH = 32;

    [Column("kind")]
    public required FigureRecordKind Kind { get; set; }

    /// <summary>A colour's <c>palette/id</c>, a kind's type, a piece's id.</summary>
    [Column("record_key")]
    [MaxLength(KEY_MAX_LENGTH)]
    public required string Key { get; set; }

    /// <summary>What it is listed under: a piece's kind of clothing, a colour's palette, a kind's own type.</summary>
    [Column("record_group")]
    [MaxLength(KEY_MAX_LENGTH)]
    public required string Group { get; set; }

    [Column("data", TypeName = "longtext")]
    public required string Data { get; set; }
}
