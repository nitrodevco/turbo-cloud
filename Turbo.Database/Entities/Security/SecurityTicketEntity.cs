using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Entities.Players;

namespace Turbo.Database.Entities.Security;

[Table("security_tickets")]
[Index(nameof(PlayerEntityId), IsUnique = true)]
[Index(nameof(Ticket), IsUnique = true)]
public class SecurityTicketEntity : TurboEntity
{
    [Column("player_id")]
    public int PlayerEntityId { get; set; }

    [Column("ticket")]
    public required string Ticket { get; set; }

    [Column("ip_address")]
    public required string IpAddress { get; set; }

    /// <summary>Reusable: a login does not use it up.</summary>
    [Column("is_locked")]
    [DefaultValue(false)]
    public bool IsLocked { get; set; }

    /// <summary>When it stops working; null for never, as a ticket written before this had.</summary>
    [Column("expires_at")]
    public DateTime? ExpiresAt { get; set; }

    [ForeignKey(nameof(PlayerEntityId))]
    public required PlayerEntity PlayerEntity { get; set; }
}
