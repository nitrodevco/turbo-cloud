using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Entities.Catalog;

namespace Turbo.Database.Entities.Players;

[Table("player_currencies")]
[Index(nameof(PlayerEntityId), nameof(Type), IsUnique = true)]
[Index(nameof(PlayerEntityId), nameof(CurrencyTypeEntityId), IsUnique = true)]
public class PlayerCurrencyEntity : TurboEntity
{
    [Column("player_id")]
    public required int PlayerEntityId { get; set; }

    [Column("type")]
    public required string Type { get; set; }

    [Column("amount")]
    [DefaultValue(0)]
    public required int Amount { get; set; }

    [Column("currency_type_id")]
    public int? CurrencyTypeEntityId { get; set; }

    [ForeignKey(nameof(PlayerEntityId))]
    public required PlayerEntity PlayerEntity { get; set; }

    [ForeignKey(nameof(CurrencyTypeEntityId))]
    public CurrencyTypeEntity? CurrencyTypeEntity { get; set; }
}
