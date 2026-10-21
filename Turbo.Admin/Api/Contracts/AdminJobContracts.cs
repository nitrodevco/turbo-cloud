using System;

namespace Turbo.Admin.Api.Contracts;

/// <summary>Work still running when its request answered (<c>202</c>): <see cref="Poll"/> is where to ask after it.</summary>
public sealed record AdminJobStartedResponse(Guid Job, string Title, string Poll);

/// <summary>
/// A job as its starter asks after it. Once <see cref="Done"/>, <see cref="AnswerStatus"/> and
/// <see cref="Answer"/> are what its request would have answered: the body on success, or the
/// usual <c>{ message }</c> when it was refused.
/// </summary>
public sealed record AdminJobResponse(
    Guid Id,
    string Title,
    bool Done,
    DateTime StartedAtUtc,
    DateTime? FinishedAtUtc,
    int? AnswerStatus,
    object? Answer
);
