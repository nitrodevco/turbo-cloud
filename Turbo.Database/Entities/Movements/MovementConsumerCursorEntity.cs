using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Turbo.Database.Entities.Movements;

/// <summary>
/// How far a consumer of the server's own has read the asset movement outbox, by its name: the
/// outbox id it has handled up to, written in the same transaction as what it made of the rows.
/// </summary>
[Table("movement_consumer_cursors")]
public class MovementConsumerCursorEntity
{
    public const int NAME_MAX_LENGTH = 64;

    [Key]
    [Column("name")]
    [MaxLength(NAME_MAX_LENGTH)]
    public required string Name { get; set; }

    [Column("outbox_id")]
    public long Cursor { get; set; }
}
