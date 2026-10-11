using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Turbo.Database.Entities.Players;

/// <summary>
/// One respect one player gave another. Giving a respect touches two players (a respect is spent
/// from one and added to the other), each in its own grain, so a crash or a retry between the two
/// could spend twice or credit twice. Each attempt gets an id once (<see cref="OperationId"/>) and
/// is recorded here before either player is touched; a retry with the same id finds the row and
/// finishes or reports it instead of doing it again. Each player's own side is kept in
/// <see cref="HumanRespectParticipantReceiptEntity"/>.
/// </summary>
[Table("player_respect_operations")]
public sealed class HumanRespectOperationEntity
{
    /// <summary>The id the attempt was given when it started; a retry carries the same one.</summary>
    [Key, MaxLength(100)]
    public required string OperationId { get; set; }

    /// <summary>The player who gave the respect.</summary>
    public int ActorId { get; set; }

    /// <summary>The player who received it.</summary>
    public int TargetId { get; set; }

    /// <summary>The giver had no respect left, so nothing was spent or given.</summary>
    public bool Rejected { get; set; }

    /// <summary>Spent from the giver and added to the receiver.</summary>
    public bool Completed { get; set; }

    /// <summary>The receiver's respect total once it was given, which a retry is answered with.</summary>
    public int ResultTotal { get; set; }
}
