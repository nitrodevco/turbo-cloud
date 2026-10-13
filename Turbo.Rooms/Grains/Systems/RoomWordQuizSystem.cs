using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Messages.Outgoing.Poll;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Rooms.Grains.Systems;

/// <summary>
/// The room's word quiz (Flash <c>WordQuizWidget</c>): a question everyone in the room answers
/// with a thumb up ("1") or down ("0") until its time is up. Each answer is shown to the room as it
/// comes (the client puts a like or unlike sign over the avatar and updates the counts), and the
/// final counts close the question.
/// <para>
/// The official client capture of 2026-10-12 (session-20261012-102507, an official public room)
/// shows the shape kept here: poll type MATCHING_POLL, question type 6, a 15000 ms duration, the
/// question's own id as both question ids, a number counting up, and a QuestionFinished whose
/// counts hold only the answers given (none answered: an empty list).
/// </para>
/// </summary>
public sealed class RoomWordQuizSystem(RoomGrain roomGrain) : RoomGrainComponent(roomGrain)
{
    public const string POLL_TYPE = "MATCHING_POLL";
    public const int QUESTION_TYPE = 6;
    public const string LIKE = "1";
    public const string DISLIKE = "0";

    /// <summary>The timer key of the open question; no room object has this id.</summary>
    private static readonly RoomObjectId TIMER_KEY = int.MinValue;

    private OpenQuestion? _open;
    private int _questionsAsked;

    public bool IsOpen => _open is not null;

    /// <summary>Puts a question to the room; false while another one is still open.</summary>
    public async Task<bool> StartAsync(string content, int durationMs, CancellationToken ct)
    {
        if (_open is not null || string.IsNullOrWhiteSpace(content) || durationMs <= 0)
            return false;

        _questionsAsked++;

        // Inference: the poll is the room's, and question ids only need to be unique in it; the
        // client matches a QuestionFinished to its question by id (WordQuizWidget.questionFinished).
        var question = new OpenQuestion(_roomGrain._state.RoomId.Value, _questionsAsked);

        _open = question;

        await _roomGrain
            .SendComposerToRoomAsync(
                new QuestionEventMessageComposer
                {
                    PollType = POLL_TYPE,
                    PollId = question.PollId,
                    QuestionId = question.QuestionId,
                    DurationMs = durationMs,
                    Number = _questionsAsked,
                    QuestionType = QUESTION_TYPE,
                    Content = content.Trim(),
                },
                ct
            )
            .ConfigureAwait(false);

        TimerSystem.Schedule(TIMER_KEY, durationMs, FinishAsync);

        return true;
    }

    /// <summary>
    /// One answer per player in the room, while the question is open; Flash's
    /// <c>WordQuizWidget.sendAnswer</c> sends a single "1" or "0".
    /// </summary>
    public async Task<bool> AnswerAsync(
        PlayerId playerId,
        int pollId,
        int questionId,
        ImmutableArray<string> answers,
        CancellationToken ct
    )
    {
        if (
            _open is not { } question
            || question.PollId != pollId
            || question.QuestionId != questionId
            || answers.Length != 1
            || answers[0] is not (LIKE or DISLIKE)
            || !AvatarModule.Players.Any(x => x.PlayerId == playerId)
            || !question.Answered.Add(playerId)
        )
            return false;

        var value = answers[0];

        question.Counts[value] = question.Counts.GetValueOrDefault(value) + 1;

        await _roomGrain
            .SendComposerToRoomAsync(
                new QuestionAnsweredEventMessageComposer
                {
                    UserId = playerId.Value,
                    Value = value,
                    AnswerCounts = question.CountsSnapshot(),
                },
                ct
            )
            .ConfigureAwait(false);

        return true;
    }

    private async Task FinishAsync(CancellationToken ct)
    {
        if (_open is not { } question)
            return;

        _open = null;

        await _roomGrain
            .SendComposerToRoomAsync(
                new QuestionFinishedEventMessageComposer
                {
                    QuestionId = question.QuestionId,
                    AnswerCounts = question.CountsSnapshot(),
                },
                ct
            )
            .ConfigureAwait(false);
    }

    private sealed class OpenQuestion(int pollId, int questionId)
    {
        public int PollId { get; } = pollId;
        public int QuestionId { get; } = questionId;
        public HashSet<PlayerId> Answered { get; } = [];

        /// <summary>Kept in the order the answers were first given.</summary>
        public Dictionary<string, int> Counts { get; } = [];

        public IReadOnlyList<KeyValuePair<string, int>> CountsSnapshot() => [.. Counts];
    }
}
