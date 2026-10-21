using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Entities.Players;

namespace Turbo.Database.Entities.WiredLink;

/// <summary>
/// A WIRED Links network: a player's named channel that the WIRED Links in their rooms connect
/// to, so a message or dataframe sent through one reaches the others. Deleting it disconnects
/// every Link on it.
/// </summary>
[Table("wired_link_networks")]
[Index(nameof(PlayerEntityId))]
public class WiredLinkNetworkEntity : TurboEntity
{
    [Column("player_id")]
    public required int PlayerEntityId { get; set; }

    [Column("name")]
    [StringLength(255)]
    public required string Name { get; set; }

    [ForeignKey(nameof(PlayerEntityId))]
    public PlayerEntity? PlayerEntity { get; set; }
}
