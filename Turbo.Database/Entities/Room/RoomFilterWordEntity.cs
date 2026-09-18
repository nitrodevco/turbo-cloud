using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Turbo.Database.Entities.Room;

[Table("room_filter_words")]
[Index(nameof(RoomEntityId), nameof(Word), IsUnique = true)]
public class RoomFilterWordEntity : TurboEntity
{
    public const int WORD_MAX_LENGTH = 64;

    [Column("room_id")]
    public required int RoomEntityId { get; set; }

    [Column("word")]
    [MaxLength(WORD_MAX_LENGTH)]
    public required string Word { get; set; }

    [ForeignKey(nameof(RoomEntityId))]
    public required RoomEntity RoomEntity { get; set; }
}
