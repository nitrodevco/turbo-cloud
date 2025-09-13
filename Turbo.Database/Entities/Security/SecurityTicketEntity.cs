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

    [Column("is_locked")]
    [DefaultValue(false)]
    public bool IsLocked { get; set; }

    [ForeignKey(nameof(PlayerEntityId))]
    public required PlayerEntity PlayerEntity { get; set; }
}
