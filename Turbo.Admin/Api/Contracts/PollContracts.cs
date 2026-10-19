using System;

namespace Turbo.Admin.Api.Contracts;

/// <summary>
/// A room poll as listed: its room, headline, whether it is on, and how far players got with it:
/// <see cref="Started"/> (opened it, not done yet), <see cref="Finished"/> and
/// <see cref="Rejected"/> (said no, so it is never offered to them again).
/// </summary>
public sealed record PollListItem(
    int Id,
    int RoomId,
    string? RoomName,
    string Headline,
    bool Enabled,
    bool NpsPoll,
    int Questions,
    int Started,
    int Finished,
    int Rejected
);

public sealed record PollsResponse(bool CanManage, PollListItem[] Polls);

/// <summary>A choice: <see cref="Value"/> is what the client answers with, <see cref="Text"/> what it shows.</summary>
public sealed record PollChoice(int? Id, string Value, string Text, int ChoiceType);

/// <summary>
/// A question. <see cref="Type"/> is <c>single</c>, <c>multiple</c> (choices), <c>line</c> or
/// <c>area</c> (text). <see cref="Parent"/> is the index, in the poll's list, of the question it
/// follows up in a score poll, asked after a choice of the same <see cref="Category"/>; null for
/// a question of its own. <see cref="Id"/> is null for a new one.
/// </summary>
public sealed record PollQuestion(
    int? Id,
    int? Parent,
    int SortOrder,
    string Type,
    string Text,
    int Category,
    int AnswerType,
    PollChoice[] Choices
);

/// <summary>A poll to save, its questions in their order. A question kept by its id keeps its answers.</summary>
public sealed record PollRequest(
    int RoomId,
    string? Type,
    string Headline,
    string Summary,
    string? StartMessage,
    string? EndMessage,
    bool NpsPoll,
    bool Enabled,
    PollQuestion[] Questions
);

/// <summary>A choice's tally: how many answers picked it.</summary>
public sealed record PollChoiceCount(string Value, int Count);

/// <summary>A text answer, newest first, with who gave it.</summary>
public sealed record PollTextAnswer(int PlayerId, string? PlayerName, string Text, DateTime AtUtc);

/// <summary>What players answered one question: how many, each choice's tally, the newest texts.</summary>
public sealed record PollQuestionResult(
    int QuestionId,
    int Answered,
    PollChoiceCount[] Choices,
    PollTextAnswer[] Texts
);

/// <summary>A poll with its questions in order, and the answers so far.</summary>
public sealed record PollDetailResponse(
    int Id,
    int RoomId,
    string? RoomName,
    string Type,
    string Headline,
    string Summary,
    string StartMessage,
    string EndMessage,
    bool NpsPoll,
    bool Enabled,
    PollQuestion[] Questions,
    int Started,
    int Finished,
    int Rejected,
    PollQuestionResult[] Results
);
