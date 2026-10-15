using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Turbo.Database.Entities.Polls;

/// <summary>
/// One choice of a single or multiple choice question (Flash <c>PollChoice</c>): the client shows
/// <see cref="Text"/> and answers with <see cref="Value"/>; in a score poll
/// <see cref="ChoiceType"/> picks the follow-up question of that category.
/// </summary>
[Table("room_poll_choices")]
[Index(nameof(QuestionId), nameof(SortOrder))]
public class RoomPollChoiceEntity : TurboEntity
{
    [Column("question_id")]
    public required int QuestionId { get; set; }

    [Column("sort_order")]
    [DefaultValue(0)]
    public int SortOrder { get; set; }

    [Column("value")]
    public required string Value { get; set; }

    [Column("text")]
    public required string Text { get; set; }

    [Column("choice_type")]
    [DefaultValue(0)]
    public int ChoiceType { get; set; }

    [ForeignKey(nameof(QuestionId))]
    public RoomPollQuestionEntity? Question { get; set; }
}
