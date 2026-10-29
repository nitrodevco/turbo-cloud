using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Turbo.Database.Entities.Furniture;

[Table("furniture_trade_receipts")]
[Index(nameof(InitiatorId), nameof(InitiatorReconciled))]
[Index(nameof(PartnerId), nameof(PartnerReconciled))]
[Index(nameof(InitiatorReconciled), nameof(PartnerReconciled), nameof(ReconcileAttemptedAt))]
public class FurnitureTradeReceiptEntity
{
    [Key]
    [Column("operation_id")]
    [MaxLength(100)]
    public required string OperationId { get; set; }

    [Column("request_hash")]
    [MaxLength(64)]
    public required string RequestHash { get; set; }

    [Column("initiator_id")]
    public int InitiatorId { get; set; }

    [Column("partner_id")]
    public int PartnerId { get; set; }

    [Column("initiator_item_ids", TypeName = "longtext")]
    public required string InitiatorItemIds { get; set; }

    [Column("partner_item_ids", TypeName = "longtext")]
    public required string PartnerItemIds { get; set; }

    [Column("initiator_reconciled")]
    public bool InitiatorReconciled { get; set; }

    [Column("partner_reconciled")]
    public bool PartnerReconciled { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("reconcile_attempted_at")]
    public DateTime ReconcileAttemptedAt { get; set; } = DateTime.UnixEpoch;
}
