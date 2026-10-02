using Turbo.Primitives.Commands;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.Tests.Commands;

[Command("finalizationprobe")]
[RequiresPermission("command.finalizationprobe")]
public sealed class FinalizationProbeCommand : IOperatorCommand<NoArguments>
{
    public Action? BeforeReturn { get; init; }

    public bool ThrowCancellation { get; init; }

    public ValueTask<CommandResult> ExecuteAsync(
        IOperatorCommandContext ctx,
        NoArguments arguments,
        CancellationToken ct
    )
    {
        BeforeReturn?.Invoke();
        if (ThrowCancellation)
            ct.ThrowIfCancellationRequested();
        return ValueTask.FromResult(CommandResult.Ok);
    }
}
