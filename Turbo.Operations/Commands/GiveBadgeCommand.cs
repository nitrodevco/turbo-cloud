using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.Operations.Commands;

public sealed record GiveBadgeArguments(
    [Selectors(PermissionNodes.Command.GIVE_MASS)] PlayerTarget Who,
    string Code
);

/// <summary>
/// <c>:givebadge name CODE</c>. Prizes and corrections. <c>@room</c> and <c>@online</c> need
/// <c>command.give.mass</c> and are always logged. The badge grain refuses a code that is not
/// well formed, and a badge the player already owns.
/// </summary>
[Command("givebadge", Description = "Give a player a badge", Category = CommandCategories.SUPPORT)]
[RequiresPermission(PermissionNodes.Command.GIVEBADGE)]
public sealed class GiveBadgeCommand(IGrainFactory grainFactory)
    : IOperatorCommand<GiveBadgeArguments>
{
    private const string GIVEN = "given";
    private const string GIVEN_MANY = "given_many";
    private const string NOT_GIVEN = "not_given";

    public IReadOnlyDictionary<string, string> DefaultTexts { get; } =
        new Dictionary<string, string>
        {
            [GIVEN] = "%0% now has the badge %1%.",
            [GIVEN_MANY] = "%0% players got the badge %1%.",
            [NOT_GIVEN] =
                "%0% was not given the badge %1%: they already have it, or it is not a badge.",
        };

    public async ValueTask<CommandResult> ExecuteAsync(
        IOperatorCommandContext ctx,
        GiveBadgeArguments arguments,
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
                    .GiveBadgeAsync(arguments.Code, token),
            ct
        );

        var completed = selection.IsSelector
            ? CommandResult.Done(
                GIVEN_MANY,
                batch.CompletedTargets.ToString(CultureInfo.InvariantCulture),
                arguments.Code
            )
            : CommandResult.Done(GIVEN, selection.Players[0].Name, arguments.Code);

        return batch.ToResult(
            completed,
            selection.IsSelector
                ? default
                : CommandResult.Fail(NOT_GIVEN, selection.Players[0].Name, arguments.Code)
        );
    }
}
