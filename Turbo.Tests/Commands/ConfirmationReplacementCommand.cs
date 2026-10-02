using Turbo.Primitives.Commands;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.Tests.Commands;

[Command("replacementprobe")]
[RequiresPermission("command.replacementprobe")]
public sealed class ConfirmationReplacementCommand : IOperatorCommand<ProbeArguments>
{
    public List<ProbeArguments> Calls { get; } = [];

    public bool AddAudienceAfterConfirmation { get; init; }

    public ValueTask<CommandResult> ExecuteAsync(
        IOperatorCommandContext ctx,
        ProbeArguments arguments,
        CancellationToken ct
    )
    {
        if (ctx.IsConfirmed && AddAudienceAfterConfirmation)
            ctx.SnapshotRecipients("late", [200]);

        Calls.Add(arguments);

        return ValueTask.FromResult(
            ctx.IsConfirmed
                ? CommandResult.Ok
                : CommandResult.Confirm(CommandReplyKeys.CONFIRM_SELECTOR, "1")
        );
    }
}
