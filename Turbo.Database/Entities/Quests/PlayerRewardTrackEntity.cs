using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Entities.Players;

namespace Turbo.Database.Entities.Quests;

/// <summary>
/// A player's progress on one reward track (the tracks themselves are configuration).
/// <see cref="ProgressJson"/> holds each task's count, the values a distinct task has counted
/// and the claimed prize ids.
/// </summary>
[Table("player_reward_tracks")]
[Index(nameof(PlayerEntityId), nameof(TrackId), IsUnique = true)]
public class PlayerRewardTrackEntity : TurboEntity
{
    [Column("player_id")]
    public required int PlayerEntityId { get; set; }

    [Column("track_id")]
    [StringLength(64)]
    public required string TrackId { get; set; }

    [Column("points")]
    [DefaultValue(0)]
    public int Points { get; set; }

    [Column("premium")]
    [DefaultValue(false)]
    public bool Premium { get; set; }

    [Column("progress_json", TypeName = "longtext")]
    [MaxLength(int.MaxValue)]
    public string ProgressJson { get; set; } = "{}";

    [ForeignKey(nameof(PlayerEntityId))]
    public PlayerEntity? PlayerEntity { get; set; }
}
