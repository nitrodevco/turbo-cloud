using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Turbo.Database.Entities.Players;

[Table("human_respect_operations")]
// The respects a player gave: the hotel API counts them (IHotelPlayers.RespectsAsync).
[Index(nameof(ActorId), nameof(Completed))]
public sealed class HumanRespectOperationEntity
{
    [Key, MaxLength(100)]
    public required string OperationId { get; set; }

    public int ActorId { get; set; }

    public int TargetId { get; set; }

    public bool Rejected { get; set; }

    public bool Completed { get; set; }

    public int ResultTotal { get; set; }
}
