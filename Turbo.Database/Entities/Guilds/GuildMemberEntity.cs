using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Entities.Players;
using Turbo.Primitives.Guilds.Enums;

namespace Turbo.Database.Entities.Guilds;

/// <summary>
/// One player's standing in one group. A pending request and a block are rows here too, told
/// apart by their rank — which is what lets a request be rejected without losing that it was
/// made, and a block outlive the kick that caused it.
/// </summary>
[Table("guild_members")]
[Index(nameof(GuildEntityId), nameof(PlayerEntityId), IsUnique = true)]
// "My groups" reads by player across every group, which the unique index above (guild first)
// cannot serve.
[Index(nameof(PlayerEntityId))]
// The member window filters one group by rank, and the pending count is the same read.
[Index(nameof(GuildEntityId), nameof(Rank))]
public class GuildMemberEntity : TurboEntity
{
    [Column("guild_id")]
    public required int GuildEntityId { get; set; }

    [Column("player_id")]
    public required int PlayerEntityId { get; set; }

    [Column("member_rank")]
    [DefaultValue(GuildMemberRank.Member)]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public required GuildMemberRank Rank { get; set; }

    /// <summary>
    /// Whether this is the player's favourite group: the badge they wear and the one their room
    /// avatar carries. At most one row per player should hold it.
    /// </summary>
    [Column("is_favourite")]
    [DefaultValue(false)]
    public required bool IsFavourite { get; set; }

    [ForeignKey(nameof(GuildEntityId))]
    public GuildEntity? GuildEntity { get; set; }

    [ForeignKey(nameof(PlayerEntityId))]
    public PlayerEntity? PlayerEntity { get; set; }
}
