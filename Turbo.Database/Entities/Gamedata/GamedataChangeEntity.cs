using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Turbo.Primitives.Gamedata.Enums;

namespace Turbo.Database.Entities.Gamedata;

/// <summary>
/// One row a change set touched. For a definition, <see cref="Before"/> and <see cref="After"/> are
/// JSON objects of the fields that changed, by furnidata key; for Habbo's furniture, its item
/// whole. A row the set made has no <see cref="Before"/>, and a row it removed no <see cref="After"/>.
/// </summary>
[Table("gamedata_changes")]
[Index(nameof(ChangeSetEntityId))]
public class GamedataChangeEntity : TurboEntity
{
    public const int LABEL_MAX_LENGTH = 128;

    [Column("change_set_id")]
    public int ChangeSetEntityId { get; set; }

    [Column("record_type")]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public required GamedataRecordType RecordType { get; set; }

    [Column("record_id")]
    public required int RecordId { get; set; }

    [Column("label")]
    [MaxLength(LABEL_MAX_LENGTH)]
    public required string Label { get; set; }

    [Column("before", TypeName = "longtext")]
    public string? Before { get; set; }

    [Column("after", TypeName = "longtext")]
    public string? After { get; set; }

    [ForeignKey(nameof(ChangeSetEntityId))]
    public GamedataChangeSetEntity? ChangeSet { get; set; }
}
