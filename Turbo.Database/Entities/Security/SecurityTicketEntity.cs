using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;
using System.Security.Cryptography;
using System.Text;
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

    /// <summary>
    /// What a login is checked against. A ticket the server issues is stored only as
    /// <see cref="Hash"/> of it, so a copy of the table logs nobody in; one written by something
    /// else (a CMS, the load-test bots) is stored as it was written and still works.
    /// </summary>
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

    /// <summary>Starts every hashed ticket, so a stored hash can never be presented as a ticket.</summary>
    public const string HASH_PREFIX = "sha256:";

    /// <summary>How an issued ticket is stored and looked up.</summary>
    public static string Hash(string ticket) =>
        HASH_PREFIX + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(ticket)));
}
