using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Entities.Players;
using Turbo.Database.Entities.Room;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Database.Entities.Pets;

[Table("pets")]
[Index(nameof(PlayerEntityId))]
[Index(nameof(RoomEntityId))]
public class PetEntity : TurboEntity
{
    public const int NAME_MAX_LENGTH = 32;
    public const int COLOR_MAX_LENGTH = 6;
    public const int CUSTOM_PARTS_MAX_LENGTH = 255;

    [Column("player_id")]
    public required int PlayerEntityId { get; set; }

    [Column("room_id")]
    public int? RoomEntityId { get; set; }

    [Column("name")]
    [MaxLength(NAME_MAX_LENGTH)]
    public required string Name { get; set; }

    [Column("type_id")]
    public required int TypeId { get; set; }

    /// <summary>The palette the figure is drawn with; picks the colour variant of a breed.</summary>
    [Column("palette_id")]
    public required int PaletteId { get; set; }

    /// <summary>The breed the palette belongs to; names the pet as <c>pet.breed.&lt;type&gt;.&lt;breed&gt;</c>.</summary>
    [Column("breed_id")]
    public required int BreedId { get; set; }

    [Column("color")]
    [MaxLength(COLOR_MAX_LENGTH)]
    public required string Color { get; set; }

    /// <summary>Custom part triples (layer, part, palette) separated by spaces.</summary>
    [Column("custom_parts")]
    [MaxLength(CUSTOM_PARTS_MAX_LENGTH)]
    public string? CustomParts { get; set; }

    [Column("level")]
    [DefaultValue(1)]
    public int Level { get; set; } = 1;

    [Column("experience")]
    [DefaultValue(0)]
    public int Experience { get; set; }

    [Column("energy")]
    [DefaultValue(100)]
    public int Energy { get; set; } = 100;

    [Column("nutrition")]
    [DefaultValue(100)]
    public int Nutrition { get; set; } = 100;

    [Column("respect")]
    [DefaultValue(0)]
    public int Respect { get; set; }

    [Column("rarity_level")]
    [DefaultValue(0)]
    public int RarityLevel { get; set; }

    [Column("has_saddle")]
    [DefaultValue(false)]
    public bool HasSaddle { get; set; }

    [Column("anyone_can_ride")]
    [DefaultValue(false)]
    public bool AnyoneCanRide { get; set; }

    [Column("breeding_permission")]
    [DefaultValue(false)]
    public bool HasBreedingPermission { get; set; }

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

    [Column("watered_at")]
    public DateTime WateredAt { get; set; }

    [Column("harvested_at")]
    public DateTime? HarvestedAt { get; set; }

    [ForeignKey(nameof(PlayerEntityId))]
    public PlayerEntity? PlayerEntity { get; set; }

    [ForeignKey(nameof(RoomEntityId))]
    public RoomEntity? RoomEntity { get; set; }
}
