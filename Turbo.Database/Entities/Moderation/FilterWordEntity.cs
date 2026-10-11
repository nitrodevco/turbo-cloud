using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Turbo.Database.Entities.Moderation;

/// <summary>
/// A word of the hotel's filter, kept out of everything players type (see <c>IWordFilter</c>).
/// One word per row, matched whole and ignoring case; a room owner's own list is
/// <c>room_filter_words</c>.
/// </summary>
[Comment("A word of the hotel's filter, kept out of everything players type (see IWordFilter).")]
[Table("moderation_filter_words")]
public class FilterWordEntity : TurboEntity
{
    public const int WORD_MAX_LENGTH = 64;

    [Column("word")]
    [MaxLength(WORD_MAX_LENGTH)]
    public required string Word { get; set; }
}
