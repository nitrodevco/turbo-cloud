using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Turbo.Primitives.Rooms.Polls;

namespace Turbo.Database.Entities.Polls;

/// <summary>
/// One question of a <see cref="RoomPollEntity"/> (Flash <c>PollQuestion</c>). A question with a
/// <see cref="ParentQuestionId"/> is a follow-up of that one: a score poll asks it only after a
/// choice of the same <see cref="Category"/>.
/// </summary>
[Table("room_poll_questions")]
[Index(nameof(PollId), nameof(SortOrder))]
public class RoomPollQuestionEntity : TurboEntity
{
    [Column("poll_id")]
    public required int PollId { get; set; }

    [Column("parent_question_id")]
    public int? ParentQuestionId { get; set; }

    [Column("sort_order")]
    [DefaultValue(0)]
    public int SortOrder { get; set; }

    [Column("question_type")]
    public required RoomPollQuestionType QuestionType { get; set; }

    [Column("text")]
    public required string Text { get; set; }

    [Column("category")]
    [DefaultValue(0)]
    public int Category { get; set; }

    [Column("answer_type")]
    [DefaultValue(0)]
    public int AnswerType { get; set; }

    [ForeignKey(nameof(PollId))]
    public RoomPollEntity? Poll { get; set; }
}
