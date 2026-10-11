using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Turbo.Database.Entities.Players;

/// <summary>
/// One player's side of a respect: the spend from the giver, or the credit to the receiver (and a
/// pet scratch's spend). Saved with the player's own change, so a respect operation that is retried
/// finds the receipt and does not spend or credit that player again. See
/// <see cref="HumanRespectOperationEntity"/>.
/// </summary>
[Table("player_respect_receipts")]
[PrimaryKey(nameof(PlayerId), nameof(OperationId), nameof(Kind))]
public sealed class HumanRespectParticipantReceiptEntity
{
    public int PlayerId { get; set; }

    /// <summary>The respect operation this is a side of (<see cref="HumanRespectOperationEntity.OperationId"/>).</summary>
    [MaxLength(100)]
    public required string OperationId { get; set; }

    /// <summary>One of <see cref="RespectReceiptKinds"/>.</summary>
    [MaxLength(16)]
    public required string Kind { get; set; }

    public bool Accepted { get; set; }

    public int ResultTotal { get; set; }
}
