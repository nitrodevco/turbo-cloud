namespace Turbo.Primitives.Commands;

/// <summary>
/// What a command reports back. Core turns it into a whisper from the hotel texts under
/// <c>command.&lt;name&gt;.&lt;status&gt;</c>, with <c>%0%</c>, <c>%1%</c>... replaced by
/// <see cref="Parameters"/>, so a hotel can reword and translate every reply.
/// </summary>
public readonly record struct CommandResult(string? Status, string[] Parameters)
{
    /// <summary>Done, and nothing to say.</summary>
    public static CommandResult Ok { get; } = new(null, []);

    /// <summary>Done, with a status the hotel texts answer.</summary>
    public static CommandResult Done(string status, params string[] parameters) =>
        new(status, parameters);

    /// <summary>Refused or failed, with a status the hotel texts answer.</summary>
    public static CommandResult Fail(string status, params string[] parameters) =>
        new(status, parameters) { IsFailure = true };

    /// <summary>
    /// Not done yet: the line reaches enough of the hotel that the executor must say
    /// <c>:confirm</c> first. The status is the command's own text for what it would do, and core
    /// adds how to go ahead.
    /// </summary>
    public static CommandResult Confirm(string status, params string[] parameters) =>
        new(status, parameters) { NeedsConfirmation = true };

    public bool IsFailure { get; init; }

    public bool NeedsConfirmation { get; init; }

    /// <summary>Per-target accounting when the command executes a batch.</summary>
    public CommandBatchResult? Batch { get; init; }

    /// <summary>The execution outcome, independent of the translated reply.</summary>
    public CommandOutcome Outcome =>
        NeedsConfirmation ? CommandOutcome.AwaitingConfirmation
        : Batch is { } batch ? batch.Outcome
        : IsFailure ? CommandOutcome.Failed
        : CommandOutcome.Completed;
}
