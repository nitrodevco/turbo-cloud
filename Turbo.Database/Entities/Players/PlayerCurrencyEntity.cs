using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Turbo.Database.Entities.Players;

[Table("player_currencies")]
[Index(nameof(PlayerEntityId), nameof(Type), IsUnique = true)]
public class PlayerCurrencyEntity : TurboEntity
{
    [Column("player_id")]
    public required int PlayerEntityId { get; set; }

    [Column("type")]
    public required string Type { get; set; }

    [Column("amount")]
    [DefaultValue(0)]
    public required int Amount { get; set; }

    [ForeignKey(nameof(PlayerEntityId))]
    public required PlayerEntity PlayerEntity { get; set; }
}
