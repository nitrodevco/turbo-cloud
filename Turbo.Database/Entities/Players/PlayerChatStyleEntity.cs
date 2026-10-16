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

    // How the client draws it: the rest of its chat-style-definitions.json entry, built into the
    // habbo-free-flow-chat-com bundle with the bitmaps below. The flags above are its
    // hcOnly (club_only), ambassadorOnly, purchasable and systemStyle (system).

    /// <summary>Staff may pick it whatever its other flags (the client's <c>staffOverrideable</c>).</summary>
    [Column("staff_overrideable")]
    [DefaultValue(false)]
    public bool StaffOverrideable { get; set; }

    /// <summary>Its lines may carry HTML (the client's <c>allowHTML</c>).</summary>
    [Column("allow_html")]
    [DefaultValue(false)]
    public bool AllowHtml { get; set; }

    /// <summary>A notification bubble: markup in its text and its icon centred (the client's <c>notification</c>).</summary>
    [Column("notification")]
    [DefaultValue(false)]
    public bool Notification { get; set; }

    /// <summary>Its reg points (nine-slice, margins, face, font...) as the client's JSON object; null before it has a look.</summary>
    [Column("reg_points", TypeName = "longtext")]
    public string? RegPoints { get; set; }

    /// <summary>Its place in the client's style picker, which lists styles in the bundle's order.</summary>
    [Column("sort_order")]
    [DefaultValue(0)]
    public int SortOrder { get; set; }

    public List<PlayerChatStyleOwnedEntity>? OwnedChatStyles { get; set; }

    public List<PlayerChatStyleBitmapEntity>? Bitmaps { get; set; }
}
