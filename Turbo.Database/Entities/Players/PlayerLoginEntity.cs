using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Net;
using Microsoft.EntityFrameworkCore;
using Turbo.Primitives.Players;

namespace Turbo.Database.Entities.Players;

/// <summary>
/// A session a player opened: from where (the address, through a trusted proxy), on what
/// machine (the id the client sends), and when it ended. Staff look here for the other accounts
/// behind a machine or an address, and for when a player was on. <c>created_at</c> is the login.
/// </summary>
[Comment("A session a player opened: from which address and machine, and when it ended.")]
[Table("player_logins")]
[Index(nameof(PlayerEntityId), nameof(Id))]
[Index(nameof(IpAddress))]
[Index(nameof(MachineId))]
public class PlayerLoginEntity : TurboLogEntity
{
    [Column("player_id")]
    public required int PlayerEntityId { get; set; }

    /// <summary>Null when the connection's address could not be told.</summary>
    [Column("ip_address")]
    public IPAddress? IpAddress { get; set; }

    /// <summary>Null when the client sent none.</summary>
    [Column("machine_id")]
    [MaxLength(PlayerLogins.MACHINE_ID_MAX_LENGTH)]
    public string? MachineId { get; set; }

    /// <summary>When the session ended; null while it is open, or if the server stopped first.</summary>
    [Column("logged_out_at")]
    public DateTime? LoggedOutAt { get; set; }
}
