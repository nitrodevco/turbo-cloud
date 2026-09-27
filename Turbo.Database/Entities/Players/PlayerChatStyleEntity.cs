using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Turbo.Database.Entities.Players;

/// <summary>
/// One chat bubble style the client can draw, and who may speak with it. A style with no row is
/// one no player may pick. The flags combine: a style that is both club-only and purchasable
/// needs a player who has the club and owns it.
/// </summary>
[Table("player_chat_styles")]
[Index(nameof(ClientStyleId), IsUnique = true)]
public class PlayerChatStyleEntity : TurboEntity
{
    [Column("client_style_id")]
    [DefaultValue(0)]
    public required int ClientStyleId { get; set; }

    /// <summary>The client's asset id for the style, for whoever reads the table.</summary>
    [Column("name")]
    [MaxLength(64)]
    public string? Name { get; set; }

    [Column("club_only")]
    [DefaultValue(false)]
    public bool ClubOnly { get; set; }

    /// <summary>Ambassadors may speak with it, and so may staff.</summary>
    [Column("ambassador_only")]
    [DefaultValue(false)]
    public bool AmbassadorOnly { get; set; }

    [Column("staff_only")]
    [DefaultValue(false)]
    public bool StaffOnly { get; set; }

    /// <summary>Only a player with a row in <c>player_chat_styles_owned</c> may speak with it.</summary>
    [Column("purchasable")]
    [DefaultValue(false)]
    public bool Purchasable { get; set; }

    /// <summary>The server's own bubbles (bots, notifications, wired); never a player's.</summary>
    [Column("system")]
    [DefaultValue(false)]
    public bool System { get; set; }

    public List<PlayerChatStyleOwnedEntity>? OwnedChatStyles { get; set; }
}
