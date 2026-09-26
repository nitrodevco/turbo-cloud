using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Entities.Players;
using Turbo.Database.Entities.Room;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Database.Entities.Bots;

[Table("bots")]
[Index(nameof(PlayerEntityId))]
[Index(nameof(RoomEntityId))]
public class BotEntity : TurboEntity, IInventoryUnitEntity
{
    public const int NAME_MAX_LENGTH = 32;
    public const int MOTTO_MAX_LENGTH = 128;
    public const int FIGURE_MAX_LENGTH = 279;
    public const int CHAT_TEXT_MAX_LENGTH = 2000;

    [Column("player_id")]
    public required int PlayerEntityId { get; set; }

    [Column("room_id")]
    public int? RoomEntityId { get; set; }

    [Column("name")]
    [MaxLength(NAME_MAX_LENGTH)]
    public required string Name { get; set; }

    [Column("motto")]
    [MaxLength(MOTTO_MAX_LENGTH)]
    public required string Motto { get; set; }

    [Column("figure")]
    [MaxLength(FIGURE_MAX_LENGTH)]
    public required string Figure { get; set; }

    [Column("gender")]
    [DefaultValue(AvatarGenderType.Male)]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public required AvatarGenderType Gender { get; set; }

    [Column("x")]
    [DefaultValue(0)]
    public int X { get; set; }

    [Column("y")]
    [DefaultValue(0)]
    public int Y { get; set; }

    [Column("z", TypeName = "double(10,3)")]
    [DefaultValue(0.0d)]
    public double Z { get; set; }

    [Column("direction")]
    [DefaultValue(Rotation.North)]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public Rotation Rotation { get; set; }

    [Column("free_roam")]
    [DefaultValue(false)]
    public bool FreeRoam { get; set; }

    [Column("chat_text")]
    [MaxLength(CHAT_TEXT_MAX_LENGTH)]
    public string? ChatText { get; set; }

    [Column("auto_chat")]
    [DefaultValue(false)]
    public bool AutoChat { get; set; }

    [Column("chat_delay")]
    [DefaultValue(0)]
    public int ChatDelaySeconds { get; set; }

    [Column("mix_sentences")]
    [DefaultValue(false)]
    public bool MixSentences { get; set; }

    [Column("dance")]
    [DefaultValue(AvatarDanceType.None)]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public AvatarDanceType DanceType { get; set; }

    [ForeignKey(nameof(PlayerEntityId))]
    public PlayerEntity? PlayerEntity { get; set; }

    [ForeignKey(nameof(RoomEntityId))]
    public RoomEntity? RoomEntity { get; set; }
}
