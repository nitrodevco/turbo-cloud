using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Turbo.Database.Entities.Achievements;

/// <summary>
/// The last authoritative state value recorded as a fact for a player and source. A state fact is
/// only recorded again when the value moved, so a login that changes nothing writes nothing.
/// </summary>
[Table("achievement_state_values")]
[PrimaryKey(nameof(PlayerId), nameof(Source))]
public sealed class AchievementStateValueEntity
{
    public int PlayerId { get; set; }

    [MaxLength(64)]
    public required string Source { get; set; }
    public long Value { get; set; }
}
