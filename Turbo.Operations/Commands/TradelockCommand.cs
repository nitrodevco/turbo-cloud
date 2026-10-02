using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.Operations.Commands;

public sealed record TradelockArguments(PlayerTarget Who, CommandDuration Duration);

/// <summary>
/// <c>:tradelock name 7d</c>. Stops a player trading, as a temporary denial of <c>trade</c>. The
/// server enforces it; the client never asks.
/// </summary>
[Command(
    "tradelock",
    Description = "Stop a player trading for a time",
    Category = CommandCategories.MODERATION
)]
[RequiresPermission(PermissionNodes.Command.TRADELOCK)]
public sealed class TradelockCommand(IGrainFactory grainFactory, TimeProvider timeProvider)
    : IOperatorCommand<TradelockArguments>
{
    private const string LOCKED = "locked";
    private const string ALREADY = "already";
    private const string PROTECTED = "protected";
    private const string FAILED = "failed";

    public IReadOnlyDictionary<string, string> DefaultTexts { get; } =
        new Dictionary<string, string>
        {
            [LOCKED] = "%0% can't trade: %1%.",
            [ALREADY] = "%0% already can't trade.",
            [PROTECTED] = "You can't trade lock %0%.",
            [FAILED] = "%0% could not be trade locked (%1%).",
        };

    public async ValueTask<CommandResult> ExecuteAsync(
        IOperatorCommandContext ctx,
        TradelockArguments arguments,
        CancellationToken ct
    )
    {
        var selection = await ctx.ResolveAsync(arguments.Who, ct);

        if (selection.Failure is { } failure)
            return failure;

        var target = selection.Players[0];

        if (
            await OperatorGuards.IsProtectedAsync(
                grainFactory,
                ctx.Executor,
                target.Id,
                PermissionNodes.Command.TRADELOCK,
                ct
            )
        )
            return CommandResult.Fail(PROTECTED, target.Name);

        var result = await PermissionDenials.DenyAsync(
            grainFactory,
            target.Id,
            PermissionNodes.TRADE,
            arguments.Duration.EndsAt(timeProvider.GetUtcNow().UtcDateTime),
            ctx.Executor.PlayerId,
            ct
        );

        if (result == PermissionChangeResultType.Changed)
            await ctx.NotifyAsync(
                target.Id,
                "command.tradelock.notice",
                "Trading has been restricted for %0%.",
                [arguments.Duration.ToString()],
                ct
            );

        return result switch
        {
            PermissionChangeResultType.Changed => CommandResult.Done(
                LOCKED,
                target.Name,
                arguments.Duration.ToString()
            ),
            PermissionChangeResultType.Unchanged => CommandResult.Fail(ALREADY, target.Name),
            _ => CommandResult.Fail(FAILED, target.Name, result.ToString()),
        };
    }
}
