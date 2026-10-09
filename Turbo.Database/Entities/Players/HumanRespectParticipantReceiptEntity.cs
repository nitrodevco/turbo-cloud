using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Turbo.Database.Entities.Players;

[Table("human_respect_participant_receipts")]
[PrimaryKey(nameof(PlayerId), nameof(OperationId), nameof(Kind))]
public sealed class HumanRespectParticipantReceiptEntity
{
    public int PlayerId { get; set; }

    [MaxLength(100)]
    public required string OperationId { get; set; }

    /// <summary>One of <see cref="RespectReceiptKinds"/>.</summary>
    [MaxLength(16)]
    public required string Kind { get; set; }

    public bool Accepted { get; set; }

    public int ResultTotal { get; set; }
}
