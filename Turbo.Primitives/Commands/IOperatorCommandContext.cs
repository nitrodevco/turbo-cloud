using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Players;

namespace Turbo.Primitives.Commands;

/// <summary>What an operator command knows about the line it is running.</summary>
public interface IOperatorCommandContext
{
    IOperatorExecutor Executor { get; }

    /// <summary>The line as typed after the name, unfiltered.</summary>
    string ArgumentText { get; }

    /// <summary>The executor has confirmed this line with <c>:confirm</c>.</summary>
    bool IsConfirmed { get; }

    /// <summary>Notifies a target after a successful change. Delivery failure is reported
    /// separately to the executor and never turns that change into a failed operation.</summary>
    Task NotifyAsync(
        PlayerId playerId,
        string textKey,
        string defaultText,
        IReadOnlyList<string> parameters,
        CancellationToken ct
    );

    /// <summary>
    /// Whether a line reaching <paramref name="players"/> must be confirmed before it runs: when
    /// it reaches at least <c>Turbo:Commands:ConfirmAtPlayers</c> and has not been confirmed. A
    /// command that answers true returns <see cref="CommandResult.Confirm"/> and does nothing.
    /// </summary>
    bool ShouldConfirm(int players);

    /// <summary>
    /// Captures one named audience before confirmation and returns that same audience afterwards.
    /// Commands use a distinct scope for each audience; permissions and domain eligibility must
    /// still be checked when the command executes. Capture every audience before returning a
    /// confirmation request; a confirmed command cannot introduce a new audience.
    /// </summary>
    IReadOnlyList<PlayerId> SnapshotRecipients(string scope, IEnumerable<PlayerId> recipients);

    /// <summary>Executes already-resolved players through core's bounded batch executor.</summary>
    Task<CommandBatchResult> ExecuteBatchAsync(
        IReadOnlyList<ResolvedPlayer> players,
        int unitsPerPlayer,
        Func<ResolvedPlayer, CancellationToken, ValueTask<bool>> operation,
        CancellationToken ct
    );

    /// <summary>
    /// The one player a target names, online or not. A selector, or a name nobody has, is a
    /// refusal the command returns as its own result.
    /// </summary>
    Task<TargetSelection> ResolveAsync(PlayerTarget target, CancellationToken ct);

    /// <summary>
    /// Everyone a target names: a player, or <c>@room</c> and <c>@online</c> when the command
    /// declares a <see cref="SelectorsAttribute"/> and the executor holds its node. Using a
    /// selector marks the use as one to log, whether or not the executor holds
    /// <c>command.log</c>.
    /// </summary>
    Task<TargetSelection> SelectAsync(PlayerTarget target, CancellationToken ct);
}
