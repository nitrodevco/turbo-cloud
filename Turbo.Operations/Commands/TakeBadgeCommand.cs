using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.Operations.Commands;

public sealed record TakeBadgeArguments(
    [Selectors(PermissionNodes.Command.GIVE_MASS)] PlayerTarget Who,
    string Code
);

/// <summary>
/// <c>:takebadge name CODE</c>. Takes a badge back. <c>@room</c> and <c>@online</c> need
/// <c>command.give.mass</c> and are always logged.
/// </summary>
[Command(
    "takebadge",
    Description = "Take a badge from a player",
    Category = CommandCategories.SUPPORT
)]
[RequiresPermission(PermissionNodes.Command.TAKEBADGE)]
public sealed class TakeBadgeCommand(IGrainFactory grainFactory)
    : IOperatorCommand<TakeBadgeArguments>
{
    private const string TAKEN = "taken";
    private const string TAKEN_MANY = "taken_many";
    private const string NOT_TAKEN = "not_taken";

    public IReadOnlyDictionary<string, string> DefaultTexts { get; } =
        new Dictionary<string, string>
        {
            [TAKEN] = "%0% no longer has the badge %1%.",
            [TAKEN_MANY] = "The badge %1% was taken from %0% players.",
            [NOT_TAKEN] = "%0% does not have the badge %1%.",
        };

    public async ValueTask<CommandResult> ExecuteAsync(
        IOperatorCommandContext ctx,
        TakeBadgeArguments arguments,
        CancellationToken ct
    )
    {
        var selection = await ctx.SelectAsync(arguments.Who, ct);

        if (selection.Failure is { } failure)
            return failure;

        var batch = await ctx.ExecuteBatchAsync(
            selection.Players,
            1,
            async (player, token) =>
                await grainFactory
                    .GetPlayerBadgeGrain(player.Id)
                    .RemoveBadgeAsync(arguments.Code, token),
            ct
        );

        await Task.WhenAll(
            batch
                .Targets.Where(x => x.Succeeded > 0)
                .Select(x =>
                    ctx.NotifyAsync(
                        x.PlayerId,
                        "command.takebadge.notice",
                        "A moderator removed badge %0% from your account.",
                        [arguments.Code],
                        ct
                    )
                )
        );

        var completed = selection.IsSelector
            ? CommandResult.Done(
                TAKEN_MANY,
                batch.CompletedTargets.ToString(CultureInfo.InvariantCulture),
                arguments.Code
            )
            : CommandResult.Done(TAKEN, selection.Players[0].Name, arguments.Code);

        return batch.ToResult(
            completed,
            selection.IsSelector
                ? default
                : CommandResult.Fail(NOT_TAKEN, selection.Players[0].Name, arguments.Code)
        );
    }
}
