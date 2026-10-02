using Turbo.Primitives.Commands;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.Tests.Commands;

[Command("telemetryfailure")]
[RequiresPermission("command.telemetryfailure")]
public sealed class TelemetryFailureCommand : ICommand<NoArguments>
{
    public ValueTask<CommandResult> ExecuteAsync(
        ICommandContext ctx,
        NoArguments arguments,
        CancellationToken ct
    ) => ValueTask.FromResult(CommandResult.Fail(CommandReplyKeys.FAILED));
}
