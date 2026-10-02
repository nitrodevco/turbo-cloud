using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.Commands;

/// <summary>
/// <c>:confirm</c>. Runs the last line the executor was asked to confirm, as long as it was asked
/// within <c>Turbo:Commands:ConfirmationSeconds</c> and in the room they are in now. Everyone holds
/// it: it can only go ahead with a line of one's own, which the executor was already allowed to
/// type, and the node is checked again when that line runs.
/// </summary>
[Command("confirm", Description = "Go ahead with the command you were asked to confirm")]
[RequiresPermission(PermissionNodes.Command.CONFIRM)]
public sealed class ConfirmCommand : IOperatorCommand<NoArguments>
{
    public IReadOnlyDictionary<string, string> DefaultTexts { get; } =
        new Dictionary<string, string>();

    public async ValueTask<CommandResult> ExecuteAsync(
        IOperatorCommandContext ctx,
        NoArguments arguments,
        CancellationToken ct
    ) =>
        ctx is OperatorCommandContext context
            ? await context.ConfirmPendingAsync(ct)
            : CommandResult.Fail(CommandReplyKeys.NOTHING_TO_CONFIRM);
}
