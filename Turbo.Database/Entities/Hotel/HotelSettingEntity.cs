using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Turbo.Database.Entities.Hotel;

/// <summary>
/// One setting for the whole hotel that staff change while it runs, such as the welcome message,
/// by its key. Each is owned by a grain that reads it once and answers from memory.
/// </summary>
[Table("hotel_settings")]
[Index(nameof(Key), IsUnique = true)]
public class HotelSettingEntity : TurboEntity
{
    public const int KEY_MAX_LENGTH = 64;
    public const int VALUE_MAX_LENGTH = 4000;

    [Column("key")]
    [StringLength(KEY_MAX_LENGTH)]
    public required string Key { get; set; }

    [Column("value")]
    [StringLength(VALUE_MAX_LENGTH)]
    public required string Value { get; set; }
}
