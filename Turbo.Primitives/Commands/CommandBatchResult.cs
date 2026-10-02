using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Turbo.Primitives.Commands;

/// <summary>A stable per-player result, including partial quantities and work never attempted.</summary>
public sealed record CommandBatchResult(
    IReadOnlyList<CommandBatchTargetResult> Targets,
    bool WasCanceled
)
{
    public int CompletedTargets => Targets.Count(x => x.Completed);

    public int PartialTargets => Targets.Count(x => x.Partial);

    public int FailedTargets => Targets.Count(x => x.Succeeded == 0 && x.Unattempted < x.Requested);

    public int UnattemptedTargets => Targets.Count(x => x.Unattempted == x.Requested);

    public int Succeeded => Targets.Sum(x => x.Succeeded);

    public int Failed => Targets.Sum(x => x.Failed);

    public int Indeterminate => Targets.Sum(x => x.Indeterminate);

    public int Unattempted => Targets.Sum(x => x.Unattempted);

    public CommandOutcome Outcome =>
        Targets.All(x => x.Completed) ? CommandOutcome.Completed
        : Succeeded > 0 ? CommandOutcome.Partial
        : WasCanceled ? CommandOutcome.Canceled
        : Indeterminate > 0 ? CommandOutcome.Error
        : CommandOutcome.Failed;

    /// <summary>Keeps normal success and single-player refusal texts, and gives incomplete batches
    /// a shared, localisable accounting of targets and quantities.</summary>
    public CommandResult ToResult(CommandResult completed, CommandResult failed)
    {
        var result =
            Outcome == CommandOutcome.Completed ? completed
            : Targets.Count == 1 && Outcome == CommandOutcome.Failed ? failed
            : new CommandResult(
                CommandReplyKeys.BATCH_RESULT,
                [
                    .. new[]
                    {
                        CompletedTargets,
                        PartialTargets,
                        FailedTargets,
                        UnattemptedTargets,
                        Succeeded,
                        Failed,
                        Indeterminate,
                        Unattempted,
                    }.Select(x => x.ToString(CultureInfo.InvariantCulture)),
                ]
            );

        return result with
        {
            Batch = this,
            IsFailure = Outcome != CommandOutcome.Completed,
        };
    }
}
