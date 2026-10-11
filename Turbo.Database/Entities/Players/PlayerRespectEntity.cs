using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Turbo.Primitives.Players.Enums;

namespace Turbo.Database.Entities.Players;

/// <summary>
/// One respect one player gave another, and the record of who respected whom. Giving one changes
/// two players, each in their own grain, so the row is written as pending before either is
/// touched: the giver spends one of today's respects, the receiver's total goes up, and the row
/// becomes given. A crash or a retry in between finds the row by its id and finishes it, and
/// <see cref="PlayerRespectChangeEntity"/> keeps either player from being changed twice.
/// </summary>
[Comment("One respect one player gave another: who, when, and whether it went through.")]
[Table("player_respects")]
[Index(nameof(GiverId), nameof(CreatedAt))]
[Index(nameof(ReceiverId), nameof(CreatedAt))]
public sealed class PlayerRespectEntity
{
    /// <summary>The respect's id, given when the giver clicked; a retry carries the same one.</summary>
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("giver_id")]
    public int GiverId { get; set; }

    [Column("receiver_id")]
    public int ReceiverId { get; set; }

    [Column("status")]
    [DefaultValue(RespectStatus.Pending)]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public RespectStatus Status { get; set; }

    /// <summary>The receiver's respect total once given, which a retry is answered with.</summary>
    [Column("receiver_total")]
    public int ReceiverTotal { get; set; }

    [Column("created_at")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public DateTime CreatedAt { get; set; }
}
