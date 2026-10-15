using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Orleans;
using Turbo.Database.Context;
using Turbo.Database.Entities.Polls;
using Turbo.Primitives.Messages.Outgoing.Poll;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Polls;

namespace Turbo.Rooms.Grains.Polls;

/// <summary>
/// A player's room polls, read from the database on each call: polls are hotel data
/// (<c>room_polls</c> with its questions and choices) and a player meets one rarely. The flow is
/// the AS3 <c>PollWidget</c>'s: an offer on entering the poll's room; Start asks for the questions,
/// Cancel and the close button turn the poll down, Later only closes the offer (nothing is sent,
/// so it is offered again on the next entry); each question is answered with one
/// <c>PollAnswer</c>, and after the last the client thanks the player itself.
/// </summary>
internal sealed class PlayerRoomPollGrain : Grain, IPlayerRoomPollGrain
{
    /// <summary>
    /// The longest text answer the client lets a player type (layout poll_answer_text_input,
    /// <c>max_chars</c> 512).
    /// </summary>
    public const int MAX_TEXT_ANSWER_LENGTH = 512;

    private readonly IDbContextFactory<TurboDbContext> _dbCtxFactory;
    private readonly IGrainFactory _grainFactory;
    private readonly PlayerRoomPollLiveState _state;

    public PlayerRoomPollGrain(
        IDbContextFactory<TurboDbContext> dbCtxFactory,
        IGrainFactory grainFactory
    )
    {
        _dbCtxFactory = dbCtxFactory;
        _grainFactory = grainFactory;
        _state = new() { PlayerId = this.GetPlayerId() };
    }

    private PlayerId PlayerId => _state.PlayerId;

    public async Task OfferAsync(RoomId roomId, CancellationToken ct)
    {
        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        var playerId = PlayerId.Value;
        var poll = await dbCtx
            .RoomPolls.AsNoTracking()
            .Where(x => x.RoomEntityId == roomId.Value && x.Enabled)
            .Where(x =>
                !dbCtx.PlayerRoomPolls.Any(p =>
                    p.PollId == x.Id
                    && p.PlayerEntityId == playerId
                    && p.State != RoomPollState.Started
                )
            )
            .OrderBy(x => x.Id)
            .FirstOrDefaultAsync(ct);

        if (poll is null)
            return;

        await _grainFactory.SendComposerToPlayerAsync(
            PlayerId,
            new PollOfferEventMessageComposer
            {
                PollId = poll.Id,
                PollType = poll.Type,
                Headline = poll.Headline,
                Summary = poll.Summary,
            },
            ct
        );
    }

    public async Task StartAsync(RoomId roomId, int pollId, CancellationToken ct)
    {
        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        var poll = await dbCtx
            .RoomPolls.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == pollId && x.Enabled, ct);
        var progress = await FindProgressAsync(dbCtx, pollId, false, ct);

        // Only the poll of the room the player is in, and only while they have not turned it
        // down or finished it: the same polls the offer goes out for.
        if (
            poll is null
            || poll.RoomEntityId != roomId.Value
            || progress is { State: not RoomPollState.Started }
        )
        {
            await _grainFactory.SendComposerToPlayerAsync(
                PlayerId,
                new PollErrorEventMessageComposer(),
                ct
            );
            return;
        }

        if (progress is null)
        {
            dbCtx.PlayerRoomPolls.Add(
                new PlayerRoomPollEntity
                {
                    PlayerEntityId = PlayerId.Value,
                    PollId = pollId,
                    State = RoomPollState.Started,
                }
            );
            await dbCtx.SaveChangesAsync(ct);
        }

        var questions = await LoadQuestionsAsync(dbCtx, pollId, ct);

        await _grainFactory.SendComposerToPlayerAsync(
            PlayerId,
            new PollContentsEventMessageComposer
            {
                PollId = poll.Id,
                StartMessage = poll.StartMessage,
                EndMessage = poll.EndMessage,
                Questions = questions,
                NpsPoll = poll.NpsPoll,
            },
            ct
        );
    }

    public async Task RejectAsync(int pollId, CancellationToken ct)
    {
        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        if (!await dbCtx.RoomPolls.AnyAsync(x => x.Id == pollId, ct))
            return;

        var progress = await FindProgressAsync(dbCtx, pollId, true, ct);

        if (progress is null)
            dbCtx.PlayerRoomPolls.Add(
                new PlayerRoomPollEntity
                {
                    PlayerEntityId = PlayerId.Value,
                    PollId = pollId,
                    State = RoomPollState.Rejected,
                }
            );
        else if (progress.State == RoomPollState.Started)
            progress.State = RoomPollState.Rejected;
        else
            return;

        await dbCtx.SaveChangesAsync(ct);
    }

    public async Task<bool> AnswerAsync(
        int pollId,
        int questionId,
        ImmutableArray<string> answers,
        CancellationToken ct
    )
    {
        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        var progress = await FindProgressAsync(dbCtx, pollId, true, ct);

        if (progress is not { State: RoomPollState.Started })
            return false;

        // From here on the answer is the open poll's, whether it is kept or not.
        var question = await dbCtx
            .RoomPollQuestions.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == questionId && x.PollId == pollId, ct);

        if (question is null)
            return true;

        var choices = await dbCtx
            .RoomPollChoices.AsNoTracking()
            .Where(x => x.QuestionId == questionId)
            .Select(x => x.Value)
            .ToListAsync(ct);

        if (!IsValidAnswer(question.QuestionType, choices, answers))
            return true;

        var playerId = PlayerId.Value;
        var answered = await dbCtx
            .RoomPollAnswers.Where(x => x.PlayerEntityId == playerId && x.PollId == pollId)
            .Select(x => x.QuestionId)
            .ToListAsync(ct);

        if (answered.Contains(questionId))
            return true;

        dbCtx.RoomPollAnswers.Add(
            new RoomPollAnswerEntity
            {
                PollId = pollId,
                QuestionId = questionId,
                PlayerEntityId = playerId,
                Answer = string.Join('\n', answers),
            }
        );
        answered.Add(questionId);

        // Finished once every top-level question has its answer: follow-ups are asked only
        // after some choices of a score poll.
        var topLevel = await dbCtx
            .RoomPollQuestions.Where(x => x.PollId == pollId && x.ParentQuestionId == null)
            .Select(x => x.Id)
            .ToListAsync(ct);

        if (topLevel.All(answered.Contains))
            progress.State = RoomPollState.Finished;

        await dbCtx.SaveChangesAsync(ct);

        return true;
    }

    /// <summary>
    /// What the client can send for each question type (<c>PollContentDialog.answerPollQuestion</c>):
    /// the one choice picked, any of the choices ticked, or one text of up to the input's length.
    /// </summary>
    private static bool IsValidAnswer(
        RoomPollQuestionType type,
        List<string> choices,
        ImmutableArray<string> answers
    ) =>
        type switch
        {
            RoomPollQuestionType.SingleChoice => answers.Length == 1
                && choices.Contains(answers[0]),
            RoomPollQuestionType.MultipleChoice => answers.Length <= choices.Count
                && answers.Distinct().Count() == answers.Length
                && answers.All(choices.Contains),
            RoomPollQuestionType.TextLine or RoomPollQuestionType.TextArea => answers.Length == 1
                && answers[0].Length <= MAX_TEXT_ANSWER_LENGTH,
            _ => false,
        };

    private async Task<PlayerRoomPollEntity?> FindProgressAsync(
        TurboDbContext dbCtx,
        int pollId,
        bool tracked,
        CancellationToken ct
    )
    {
        var playerId = PlayerId.Value;
        var rows = tracked ? dbCtx.PlayerRoomPolls : dbCtx.PlayerRoomPolls.AsNoTracking();

        return await rows.FirstOrDefaultAsync(
            x => x.PlayerEntityId == playerId && x.PollId == pollId,
            ct
        );
    }

    private static async Task<ImmutableArray<RoomPollQuestionSnapshot>> LoadQuestionsAsync(
        TurboDbContext dbCtx,
        int pollId,
        CancellationToken ct
    )
    {
        var questions = await dbCtx
            .RoomPollQuestions.AsNoTracking()
            .Where(x => x.PollId == pollId)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Id)
            .ToListAsync(ct);
        var questionIds = questions.Select(x => x.Id).ToList();
        var choices = (
            await dbCtx
                .RoomPollChoices.AsNoTracking()
                .Where(x => questionIds.Contains(x.QuestionId))
                .OrderBy(x => x.SortOrder)
                .ThenBy(x => x.Id)
                .ToListAsync(ct)
        ).ToLookup(x => x.QuestionId);

        RoomPollQuestionSnapshot ToSnapshot(RoomPollQuestionEntity question) =>
            new()
            {
                QuestionId = question.Id,
                SortOrder = question.SortOrder,
                QuestionType = question.QuestionType,
                Text = question.Text,
                Category = question.Category,
                AnswerType = question.AnswerType,
                Choices =
                [
                    .. choices[question.Id]
                        .Select(x => new RoomPollChoiceSnapshot
                        {
                            Value = x.Value,
                            Text = x.Text,
                            ChoiceType = x.ChoiceType,
                        }),
                ],
                Children =
                [
                    .. questions.Where(x => x.ParentQuestionId == question.Id).Select(ToSnapshot),
                ],
            };

        return [.. questions.Where(x => x.ParentQuestionId is null).Select(ToSnapshot)];
    }
}

/// <summary>Whose polls these are; nothing else is kept between calls.</summary>
internal sealed class PlayerRoomPollLiveState
{
    public PlayerId PlayerId { get; set; }
}
