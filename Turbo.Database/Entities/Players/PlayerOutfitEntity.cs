using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Database.Entities.Players;

[Table("player_outfits")]
[Index(nameof(PlayerEntityId), nameof(SlotId), IsUnique = true)]
public class PlayerOutfitEntity : TurboEntity
{
    public const int FIGURE_MAX_LENGTH = 100;

    [Column("player_id")]
    public required int PlayerEntityId { get; set; }

    [Column("slot_id")]
    public required int SlotId { get; set; }

    [Column("figure")]
    [MaxLength(FIGURE_MAX_LENGTH)]
    public required string Figure { get; set; }

    [Column("gender")]
    [DefaultValue(AvatarGenderType.Male)]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public required AvatarGenderType Gender { get; set; }

    [ForeignKey(nameof(PlayerEntityId))]
    public PlayerEntity? PlayerEntity { get; set; }
}
