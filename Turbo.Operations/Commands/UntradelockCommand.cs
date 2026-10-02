using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.Operations.Commands;

public sealed record UntradelockArguments(PlayerTarget Who);

/// <summary>
/// <c>:untradelock name</c>. Lifts a trade lock, however it was given. Whoever may lock may lift.
/// </summary>
[Command(
    "untradelock",
    Description = "Let a trade locked player trade again",
    Category = CommandCategories.MODERATION
)]
[RequiresPermission(PermissionNodes.Command.TRADELOCK)]
public sealed class UntradelockCommand(IGrainFactory grainFactory)
    : IOperatorCommand<UntradelockArguments>
{
    private const string LIFTED = "lifted";
    private const string NOT_LOCKED = "not_locked";

    public IReadOnlyDictionary<string, string> DefaultTexts { get; } =
        new Dictionary<string, string>
        {
            [LIFTED] = "Removed a trading lock from %0%.",
            [NOT_LOCKED] = "%0% is not trade locked.",
        };

    public async ValueTask<CommandResult> ExecuteAsync(
        IOperatorCommandContext ctx,
        UntradelockArguments arguments,
        CancellationToken ct
    )
    {
        var selection = await ctx.ResolveAsync(arguments.Who, ct);

        if (selection.Failure is { } failure)
            return failure;

        var target = selection.Players[0];

        var changed = await PermissionDenials.LiftAsync(
            grainFactory,
            target.Id,
            PermissionNodes.TRADE,
            ctx.Executor.PlayerId,
            ct
        );
        if (!changed)
            return CommandResult.Fail(NOT_LOCKED, target.Name);

        await ctx.NotifyAsync(
            target.Id,
            "command.untradelock.notice",
            "A moderator has removed your trading lock.",
            [],
            ct
        );
        return CommandResult.Done(LIFTED, target.Name);
    }
}
