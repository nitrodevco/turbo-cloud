using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.Operations.Commands;

public sealed record UnsilenceArguments(PlayerTarget Who);

/// <summary>
/// <c>:unsilence name</c>. Lifts a hotel silence, however it was given. Whoever may silence may
/// lift it, so there is no node of its own to hand out.
/// </summary>
[Command(
    "unsilence",
    Description = "Let a silenced player speak again",
    Category = CommandCategories.MODERATION
)]
[RequiresPermission(PermissionNodes.Command.SILENCE)]
public sealed class UnsilenceCommand(IGrainFactory grainFactory)
    : IOperatorCommand<UnsilenceArguments>
{
    private const string LIFTED = "lifted";
    private const string NOT_SILENCED = "not_silenced";

    public IReadOnlyDictionary<string, string> DefaultTexts { get; } =
        new Dictionary<string, string>
        {
            [LIFTED] = "Removed the hotel silence from %0%.",
            [NOT_SILENCED] = "%0% is not silenced.",
        };

    public async ValueTask<CommandResult> ExecuteAsync(
        IOperatorCommandContext ctx,
        UnsilenceArguments arguments,
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
            PermissionNodes.Chat.SPEAK,
            ctx.Executor.PlayerId,
            ct
        );
        if (!changed)
            return CommandResult.Fail(NOT_SILENCED, target.Name);

        await ctx.NotifyAsync(
            target.Id,
            "command.unsilence.notice",
            "A moderator has removed your hotel silence.",
            [],
            ct
        );
        return CommandResult.Done(LIFTED, target.Name);
    }
}
