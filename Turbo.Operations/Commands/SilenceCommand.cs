using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.Operations.Commands;

public sealed record SilenceArguments(PlayerTarget Who, CommandDuration Duration);

/// <summary>
/// <c>:silence name 2h</c>. Stops a player speaking anywhere in the hotel, as a temporary denial
/// of <c>chat.speak</c>, which the permission grains expire and audit. The player is told, since
/// a silence with no explanation looks like a fault.
/// </summary>
[Command(
    "silence",
    Description = "Stop a player speaking anywhere in the hotel",
    Category = CommandCategories.MODERATION
)]
[RequiresPermission(PermissionNodes.Command.SILENCE)]
public sealed class SilenceCommand(IGrainFactory grainFactory, TimeProvider timeProvider)
    : IOperatorCommand<SilenceArguments>
{
    private const string SILENCED = "silenced";
    private const string ALREADY = "already";
    private const string PROTECTED = "protected";
    private const string FAILED = "failed";
    private const string NOTICE_KEY = "command.silence.notice";
    private const string DEFAULT_NOTICE = "You have been silenced: %0%.";

    public IReadOnlyDictionary<string, string> DefaultTexts { get; } =
        new Dictionary<string, string>
        {
            [SILENCED] = "%0% is silenced: %1%.",
            [ALREADY] = "%0% is already silenced.",
            [PROTECTED] = "You can't silence %0%.",
            [FAILED] = "%0% could not be silenced (%1%).",
        };

    public async ValueTask<CommandResult> ExecuteAsync(
        IOperatorCommandContext ctx,
        SilenceArguments arguments,
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
                PermissionNodes.Command.SILENCE,
                ct
            )
        )
            return CommandResult.Fail(PROTECTED, target.Name);

        var result = await PermissionDenials.DenyAsync(
            grainFactory,
            target.Id,
            PermissionNodes.Chat.SPEAK,
            arguments.Duration.EndsAt(timeProvider.GetUtcNow().UtcDateTime),
            ctx.Executor.PlayerId,
            ct
        );

        if (result == PermissionChangeResultType.Unchanged)
            return CommandResult.Fail(ALREADY, target.Name);

        if (result != PermissionChangeResultType.Changed)
            return CommandResult.Fail(FAILED, target.Name, result.ToString());

        await ctx.NotifyAsync(
            target.Id,
            NOTICE_KEY,
            DEFAULT_NOTICE,
            [arguments.Duration.ToString()],
            ct
        );

        return CommandResult.Done(SILENCED, target.Name, arguments.Duration.ToString());
    }
}
