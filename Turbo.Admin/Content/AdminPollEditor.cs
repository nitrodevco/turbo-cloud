using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Turbo.Admin.Api.Contracts;
using Turbo.Database.Context;
using Turbo.Database.Entities.Polls;
using Turbo.Primitives.Rooms.Polls;

namespace Turbo.Admin.Content;

/// <summary>
/// The room polls as staff keep them in the panel: a poll is offered to each player entering its
/// room until they finish or reject it, so a poll is a room's, with its questions in order.
/// Saving keeps a question by its id, and with it the answers given to it; a question left out
/// is taken away with its answers. Choices are kept by the value players answer with, so they
/// are written again whole. Polls are read on every room entry, start and answer, so a change
/// reaches players at once. Every change is logged with who made it.
/// </summary>
public sealed class AdminPollEditor(
    IDbContextFactory<TurboDbContext> database,
    ILogger<AdminPollEditor> logger
)
{
    public const string SINGLE = "single";
    public const string MULTIPLE = "multiple";
    public const string LINE = "line";
    public const string AREA = "area";

    /// <summary>The longest text a poll, a question or a choice takes.</summary>
    public const int TEXT_MAX_LENGTH = 1000;

    /// <summary>The text answers shown per question, newest first.</summary>
    private const int TEXTS_SHOWN = 25;

    public async Task<PollsResponse> ListAsync(bool canManage, CancellationToken ct)
    {
        var db = await database.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        var polls = await db
            .RoomPolls.AsNoTracking()
            .OrderByDescending(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.RoomEntityId,
                RoomName = x.RoomEntity!.Name,
                x.Headline,
                x.Enabled,
                x.NpsPoll,
            })
            .ToListAsync(ct)
            .ConfigureAwait(false);
        var questions = await db
            .RoomPollQuestions.AsNoTracking()
            .GroupBy(x => x.PollId)
            .Select(x => new { Id = x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.Id, x => x.Count, ct)
            .ConfigureAwait(false);
        var states = await db
            .PlayerRoomPolls.AsNoTracking()
            .GroupBy(x => new { x.PollId, x.State })
            .Select(x => new
            {
                x.Key.PollId,
                x.Key.State,
                Count = x.Count(),
            })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        int Count(int poll, RoomPollState state) =>
            states.Where(x => x.PollId == poll && x.State == state).Sum(x => x.Count);

        return new PollsResponse(
            canManage,
            [
                .. polls.Select(x => new PollListItem(
                    x.Id,
                    x.RoomEntityId,
                    x.RoomName,
                    x.Headline,
                    x.Enabled,
                    x.NpsPoll,
                    questions.GetValueOrDefault(x.Id),
                    Count(x.Id, RoomPollState.Started),
                    Count(x.Id, RoomPollState.Finished),
                    Count(x.Id, RoomPollState.Rejected)
                )),
            ]
        );
    }

    /// <summary>One poll with its questions and the answers so far; null when there is none.</summary>
    public async Task<PollDetailResponse?> GetAsync(int id, CancellationToken ct)
    {
        var db = await database.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        var poll = await db
            .RoomPolls.AsNoTracking()
            .Include(x => x.RoomEntity)
            .FirstOrDefaultAsync(x => x.Id == id, ct)
            .ConfigureAwait(false);

        if (poll is null)
            return null;

        var questions = await db
            .RoomPollQuestions.AsNoTracking()
            .Where(x => x.PollId == id)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Id)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        var questionIds = questions.Select(x => x.Id).ToList();
        var choices = await db
            .RoomPollChoices.AsNoTracking()
            .Where(x => questionIds.Contains(x.QuestionId))
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Id)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        var answers = await db
            .RoomPollAnswers.AsNoTracking()
            .Where(x => x.PollId == id)
            .OrderByDescending(x => x.Id)
            .Select(x => new
            {
                x.QuestionId,
                x.PlayerEntityId,
                PlayerName = x.PlayerEntity!.Name,
                x.Answer,
                x.CreatedAt,
            })
            .ToListAsync(ct)
            .ConfigureAwait(false);
        var states = await db
            .PlayerRoomPolls.AsNoTracking()
            .Where(x => x.PollId == id)
            .GroupBy(x => x.State)
            .Select(x => new { State = x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.State, x => x.Count, ct)
            .ConfigureAwait(false);
        var index = questions.Select((x, i) => (x.Id, i)).ToDictionary(x => x.Id, x => x.i);

        return new PollDetailResponse(
            poll.Id,
            poll.RoomEntityId,
            poll.RoomEntity?.Name,
            poll.Type,
            poll.Headline,
            poll.Summary,
            poll.StartMessage,
            poll.EndMessage,
            poll.NpsPoll,
            poll.Enabled,
            [
                .. questions.Select(x => new PollQuestion(
                    x.Id,
                    x.ParentQuestionId is { } parent && index.TryGetValue(parent, out var at)
                        ? at
                        : null,
                    x.SortOrder,
                    TypeName(x.QuestionType),
                    x.Text,
                    x.Category,
                    x.AnswerType,
                    [
                        .. choices
                            .Where(y => y.QuestionId == x.Id)
                            .Select(y => new PollChoice(y.Id, y.Value, y.Text, y.ChoiceType)),
                    ]
                )),
            ],
            states.GetValueOrDefault(RoomPollState.Started),
            states.GetValueOrDefault(RoomPollState.Finished),
            states.GetValueOrDefault(RoomPollState.Rejected),
            [
                .. questions.Select(question =>
                {
                    var given = answers.Where(x => x.QuestionId == question.Id).ToList();
                    var picked = given
                        .SelectMany(x =>
                            x.Answer.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                        )
                        .GroupBy(x => x, StringComparer.Ordinal)
                        .ToDictionary(x => x.Key, x => x.Count(), StringComparer.Ordinal);
                    var text =
                        question.QuestionType
                        is RoomPollQuestionType.TextLine
                            or RoomPollQuestionType.TextArea;

                    return new PollQuestionResult(
                        question.Id,
                        given.Count,
                        text
                            ? []
                            :
                            [
                                .. choices
                                    .Where(x => x.QuestionId == question.Id)
                                    .Select(x => new PollChoiceCount(
                                        x.Value,
                                        picked.GetValueOrDefault(x.Value)
                                    )),
                            ],
                        text
                            ?
                            [
                                .. given
                                    .Take(TEXTS_SHOWN)
                                    .Select(x => new PollTextAnswer(
                                        x.PlayerEntityId,
                                        x.PlayerName,
                                        x.Answer,
                                        x.CreatedAt
                                    )),
                            ]
                            : []
                    );
                }),
            ]
        );
    }

    /// <summary>Saves a poll, a new one when <paramref name="id"/> is 0; its id.</summary>
    /// <exception cref="ArgumentException">It isn't a poll players can answer.</exception>
    public async Task<int> SaveAsync(
        int id,
        PollRequest request,
        string actor,
        CancellationToken ct
    )
    {
        Check(request);

        var db = await database.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        if (!await db.Rooms.AnyAsync(x => x.Id == request.RoomId, ct).ConfigureAwait(false))
            throw new ArgumentException($"There is no room {request.RoomId}.");

        var poll =
            id == 0
                ? null
                : await db.RoomPolls.FirstOrDefaultAsync(x => x.Id == id, ct).ConfigureAwait(false)
                    ?? throw new ArgumentException("There is no such poll.");
        var existing = poll is null
            ? []
            : await db
                .RoomPollQuestions.Where(x => x.PollId == poll.Id)
                .ToListAsync(ct)
                .ConfigureAwait(false);

        foreach (var question in request.Questions)
            if (question.Id is { } kept && existing.All(x => x.Id != kept))
                throw new ArgumentException("A question isn't this poll's.");

        var transaction = await db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
        await using var transactionScope = transaction.ConfigureAwait(false);

        poll ??= db
            .RoomPolls.Add(
                new()
                {
                    RoomEntityId = request.RoomId,
                    Headline = "",
                    Summary = "",
                }
            )
            .Entity;
        poll.RoomEntityId = request.RoomId;
        poll.Type = (request.Type ?? string.Empty).Trim();
        poll.Headline = request.Headline.Trim();
        poll.Summary = request.Summary.Trim();
        poll.StartMessage = (request.StartMessage ?? string.Empty).Trim();
        poll.EndMessage = (request.EndMessage ?? string.Empty).Trim();
        poll.NpsPoll = request.NpsPoll;
        poll.Enabled = request.Enabled;

        // The questions left out go, with their answers.
        db.RoomPollQuestions.RemoveRange(
            existing.Where(x => request.Questions.All(y => y.Id != x.Id))
        );
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        var rows = new List<RoomPollQuestionEntity>();

        foreach (var question in request.Questions)
        {
            var row =
                existing.FirstOrDefault(x => x.Id == question.Id)
                ?? db.RoomPollQuestions.Add(
                    new()
                    {
                        PollId = poll.Id,
                        QuestionType = RoomPollQuestionType.SingleChoice,
                        Text = "",
                    }
                ).Entity;

            row.SortOrder = question.SortOrder;
            row.QuestionType = TypeOf(question.Type);
            row.Text = question.Text.Trim();
            row.Category = question.Category;
            row.AnswerType = question.AnswerType;
            rows.Add(row);
        }

        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        var ids = rows.Select(x => x.Id).ToList();

        await db
            .RoomPollChoices.Where(x => ids.Contains(x.QuestionId))
            .ExecuteDeleteAsync(ct)
            .ConfigureAwait(false);

        for (var i = 0; i < rows.Count; i++)
        {
            var question = request.Questions[i];

            rows[i].ParentQuestionId = question.Parent is { } parent ? rows[parent].Id : null;

            db.RoomPollChoices.AddRange(
                question.Choices.Select(
                    (x, at) =>
                        new RoomPollChoiceEntity
                        {
                            QuestionId = rows[i].Id,
                            SortOrder = at,
                            Value = x.Value.Trim(),
                            Text = x.Text.Trim(),
                            ChoiceType = x.ChoiceType,
                        }
                )
            );
        }

        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        await transaction.CommitAsync(ct).ConfigureAwait(false);

        logger.LogInformation(
            "Room poll {PollId} of room {RoomId} {What} by {Actor}",
            poll.Id,
            poll.RoomEntityId,
            id == 0 ? "added" : "changed",
            actor
        );

        return poll.Id;
    }

    /// <summary>Takes a poll away, its questions, answers and who answered; false when there is none.</summary>
    public async Task<bool> RemoveAsync(int id, string actor, CancellationToken ct)
    {
        var db = await database.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        var poll = await db
            .RoomPolls.FirstOrDefaultAsync(x => x.Id == id, ct)
            .ConfigureAwait(false);

        if (poll is null)
            return false;

        db.RoomPolls.Remove(poll);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        logger.LogInformation("Room poll {PollId} removed by {Actor}", id, actor);

        return true;
    }

    /// <summary>Refuses a poll players couldn't answer, with why.</summary>
    private static void Check(PollRequest request)
    {
        Text(request.Headline, "The headline", required: true);
        Text(request.Summary, "The summary", required: true);
        Text(request.StartMessage, "The start message");
        Text(request.EndMessage, "The end message");

        if (request.Questions.Length == 0)
            throw new ArgumentException("A poll needs a question.");

        for (var i = 0; i < request.Questions.Length; i++)
        {
            var question = request.Questions[i];
            var name = $"Question {i + 1}";

            Text(question.Text, name, required: true);

            var choosing =
                TypeOf(question.Type)
                is RoomPollQuestionType.SingleChoice
                    or RoomPollQuestionType.MultipleChoice;

            if (choosing && question.Choices.Length < 2)
                throw new ArgumentException($"{name} needs at least two choices.");

            if (!choosing && question.Choices.Length > 0)
                throw new ArgumentException($"{name} is answered in text, so it has no choices.");

            foreach (var choice in question.Choices)
            {
                Text(
                    choice.Value,
                    $"A choice's value in {name.ToLowerInvariant()}",
                    required: true
                );
                Text(choice.Text, $"A choice's text in {name.ToLowerInvariant()}", required: true);

                // The answer keeps the values a line each.
                if (choice.Value.Contains('\n', StringComparison.Ordinal))
                    throw new ArgumentException(
                        $"A choice's value in {name.ToLowerInvariant()} is one line."
                    );
            }

            if (
                question
                    .Choices.Select(x => x.Value.Trim())
                    .Distinct(StringComparer.Ordinal)
                    .Count() != question.Choices.Length
            )
                throw new ArgumentException($"{name}'s choices each need a value of their own.");

            if (question.Parent is not { } parent)
                continue;

            if (!request.NpsPoll)
                throw new ArgumentException(
                    $"{name} follows up another, which only a score poll does."
                );

            if (
                parent < 0
                || parent >= request.Questions.Length
                || parent == i
                || request.Questions[parent].Parent is not null
            )
                throw new ArgumentException(
                    $"{name} follows up a question of its own, not another follow-up."
                );
        }
    }

    private static void Text(string? given, string what, bool required = false)
    {
        var text = (given ?? string.Empty).Trim();

        if (required && text.Length == 0)
            throw new ArgumentException($"{what} can't be empty.");

        if (text.Length > TEXT_MAX_LENGTH)
            throw new ArgumentException($"{what} is {TEXT_MAX_LENGTH} characters at most.");
    }

    private static RoomPollQuestionType TypeOf(string type) =>
        type switch
        {
            SINGLE => RoomPollQuestionType.SingleChoice,
            MULTIPLE => RoomPollQuestionType.MultipleChoice,
            LINE => RoomPollQuestionType.TextLine,
            AREA => RoomPollQuestionType.TextArea,
            _ => throw new ArgumentException("A question is single, multiple, line or area."),
        };

    private static string TypeName(RoomPollQuestionType type) =>
        type switch
        {
            RoomPollQuestionType.MultipleChoice => MULTIPLE,
            RoomPollQuestionType.TextLine => LINE,
            RoomPollQuestionType.TextArea => AREA,
            _ => SINGLE,
        };
}
