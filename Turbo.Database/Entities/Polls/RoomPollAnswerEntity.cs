using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Entities.Players;

namespace Turbo.Database.Entities.Polls;

/// <summary>
/// A player's answer to one poll question: the choice values picked, or the text written, one
/// per line.
/// </summary>
[Table("room_poll_answers")]
[Index(nameof(PlayerEntityId), nameof(QuestionId), IsUnique = true)]
[Index(nameof(PollId))]
public class RoomPollAnswerEntity : TurboEntity
{
    [Column("poll_id")]
    public required int PollId { get; set; }

    [Column("question_id")]
    public required int QuestionId { get; set; }

    [Column("player_id")]
    public required int PlayerEntityId { get; set; }

    [Column("answer", TypeName = "text")]
    public required string Answer { get; set; }

    [ForeignKey(nameof(PollId))]
    public RoomPollEntity? Poll { get; set; }

    [ForeignKey(nameof(QuestionId))]
    public RoomPollQuestionEntity? Question { get; set; }

    [ForeignKey(nameof(PlayerEntityId))]
    public PlayerEntity? PlayerEntity { get; set; }
}
