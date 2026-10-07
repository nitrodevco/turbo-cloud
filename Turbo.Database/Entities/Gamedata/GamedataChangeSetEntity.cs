using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Turbo.Primitives.Gamedata.Enums;

namespace Turbo.Database.Entities.Gamedata;

/// <summary>
/// Gamedata changes made together - an import, an edit, a rollback - and rolled back together.
/// </summary>
[Table("gamedata_change_sets")]
public class GamedataChangeSetEntity : TurboEntity
{
    public const int SUMMARY_MAX_LENGTH = 255;

    [Column("kind")]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public required GamedataChangeKind Kind { get; set; }

    [Column("summary")]
    [MaxLength(SUMMARY_MAX_LENGTH)]
    public required string Summary { get; set; }

    /// <summary>The staff member who made it; null for one the server made on its own.</summary>
    [Column("player_id")]
    public int? PlayerEntityId { get; set; }

    [Column("release_id")]
    public int? HabboReleaseEntityId { get; set; }

    /// <summary>The set a rollback undid.</summary>
    [Column("reverts_id")]
    public int? RevertsEntityId { get; set; }

    /// <summary>The rollback that undid this set.</summary>
    [Column("rolled_back_by_id")]
    public int? RolledBackByEntityId { get; set; }

    public List<GamedataChangeEntity>? Changes { get; set; }
}
