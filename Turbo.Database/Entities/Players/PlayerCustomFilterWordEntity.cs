using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Turbo.Database.Entities.Players;

/// <summary>A word a player keeps in their own word filter (the toolbar's Word Filter window).</summary>
[Table("player_custom_filter_words")]
[Index(nameof(PlayerEntityId), nameof(Word), IsUnique = true)]
public class PlayerCustomFilterWordEntity : TurboEntity
{
    public const int WORD_MAX_LENGTH = 64;

    [Column("player_id")]
    public required int PlayerEntityId { get; set; }

    [Column("word")]
    [MaxLength(WORD_MAX_LENGTH)]
    public required string Word { get; set; }

    [ForeignKey(nameof(PlayerEntityId))]
    public required PlayerEntity PlayerEntity { get; set; }
}
