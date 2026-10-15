namespace Turbo.Primitives.Rooms.Polls;

/// <summary>
/// How a poll question is answered (Flash <c>PollContentDialog.nextQuestion</c>: questionType - 1
/// picks the input). Only the two choice types carry their choices (<c>PollContentsParser</c>).
/// </summary>
public enum RoomPollQuestionType
{
    SingleChoice = 1,
    MultipleChoice = 2,
    TextLine = 3,
    TextArea = 4,
}
