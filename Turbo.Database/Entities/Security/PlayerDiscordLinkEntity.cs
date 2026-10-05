using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Entities.Players;

namespace Turbo.Database.Entities.Security;

/// <summary>
/// The Discord account a player signs in to the public site with: one each way, so a Discord
/// account is one player and a player is one Discord account.
/// </summary>
[Table("player_discord_links")]
[Index(nameof(PlayerEntityId), IsUnique = true)]
[Index(nameof(DiscordId), IsUnique = true)]
public class PlayerDiscordLinkEntity : TurboEntity
{
    public const int DISCORD_ID_MAX_LENGTH = 32;
    public const int DISCORD_NAME_MAX_LENGTH = 64;

    [Column("player_id")]
    public required int PlayerEntityId { get; set; }

    /// <summary>Discord's id for the account, a snowflake, kept as text as Discord sends it.</summary>
    [Column("discord_id")]
    [StringLength(DISCORD_ID_MAX_LENGTH)]
    public required string DiscordId { get; set; }

    /// <summary>Their Discord username as of their last sign-in, for staff to recognise them.</summary>
    [Column("discord_username")]
    [StringLength(DISCORD_NAME_MAX_LENGTH)]
    public required string DiscordUsername { get; set; }

    [ForeignKey(nameof(PlayerEntityId))]
    public required PlayerEntity PlayerEntity { get; set; }
}
