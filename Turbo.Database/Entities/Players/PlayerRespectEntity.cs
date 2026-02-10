using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Turbo.Database.Entities.Players;

[Table("player_respects")]
[Index(nameof(PlayerEntityId), IsUnique = true)]
public class PlayerRespectEntity : TurboEntity
{
    [Column("player_id")]
    public required int PlayerEntityId { get; set; }

    [Column("respect_total")]
    [DefaultValue(0)]
    public int RespectTotal { get; set; }

    [Column("respect_left")]
    [DefaultValue(0)]
    public int RespectLeft { get; set; }

    [Column("pet_respect_left")]
    [DefaultValue(0)]
    public int PetRespectLeft { get; set; }

    [Column("respect_replenishes_left")]
    [DefaultValue(1)]
    public int RespectReplenishesLeft { get; set; }

    [Column("last_respect_reset")]
    public DateTime? LastRespectReset { get; set; }

    [ForeignKey(nameof(PlayerEntityId))]
    public PlayerEntity? PlayerEntity { get; set; }
}
