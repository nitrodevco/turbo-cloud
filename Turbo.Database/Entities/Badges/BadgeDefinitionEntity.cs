using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Turbo.Primitives.Badges.Enums;

namespace Turbo.Database.Entities.Badges;

/// <summary>
/// What the hotel says about a badge code. A badge needs no row to exist or be given; a row is
/// only there to pin its rarity (a staff badge is unique however many staff there are) instead of
/// letting the owner count decide.
/// </summary>
[Table("badge_definitions")]
[Index(nameof(BadgeCode), IsUnique = true)]
public class BadgeDefinitionEntity : TurboEntity
{
    public const int BADGE_CODE_MAX_LENGTH = 64;

    [Column("badge_code")]
    [MaxLength(BADGE_CODE_MAX_LENGTH)]
    public required string BadgeCode { get; set; }

    /// <summary>Null leaves the rarity to the owner count.</summary>
    [Column("rarity")]
    public BadgeRarityType? Rarity { get; set; }
}
