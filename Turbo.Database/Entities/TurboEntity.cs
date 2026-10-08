using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Turbo.Database.Entities;

/// <summary>
/// A row with an id and the time MySQL created it. Rows are deleted outright, so there is no
/// soft-delete column; an entity that needs to know when it last changed declares its own
/// <c>updated_at</c>.
/// </summary>
public class TurboEntity
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("created_at")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public DateTime CreatedAt { get; set; }
}
