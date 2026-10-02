using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Operations.Configuration;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.Operations.Commands;

public sealed record GiveItemArguments(
    [Selectors(PermissionNodes.Command.GIVE_MASS)] PlayerTarget Who,
    [Suggest(SuggestionSources.FURNI)] string Furni,
    int Count = 1
);

/// <summary>
/// <c>:giveitem name furni_name [count]</c>. Puts furniture in a player's inventory, to restore
/// what a bug took. The furni is named as its definition is, in the furniture data. <c>@room</c>
/// and <c>@online</c> need <c>command.give.mass</c> and are always logged. The count is capped by
/// <c>Turbo:Operations:MaxGiveItemCount</c>.
/// </summary>
[Command(
    "giveitem",
    Description = "Put furniture in a player's inventory",
    Category = CommandCategories.SUPPORT
)]
[RequiresPermission(PermissionNodes.Command.GIVEITEM)]
public sealed class GiveItemCommand(
    IGrainFactory grainFactory,
    IFurnitureDefinitionProvider definitionProvider,
    IOptions<OperationsConfig> config
) : IOperatorCommand<GiveItemArguments>
{
    private const string GIVEN = "given";
    private const string GIVEN_MANY = "given_many";
    private const string FAILED = "failed";
    private const string UNKNOWN_FURNI = "unknown_furni";
    private const string BAD_COUNT = "bad_count";

    public IReadOnlyDictionary<string, string> DefaultTexts { get; } =
        new Dictionary<string, string>
        {
            [GIVEN] = "%0% was given %1% x %2%.",
            [GIVEN_MANY] = "%0% players were given %1% x %2%.",
            [FAILED] = "%0% could not be given %2%.",
            [UNKNOWN_FURNI] = "There is no furniture called %0%.",
            [BAD_COUNT] = "The count must be between 1 and %0%.",
        };

    public async ValueTask<CommandResult> ExecuteAsync(
        IOperatorCommandContext ctx,
        GiveItemArguments arguments,
        CancellationToken ct
    )
    {
        var max = config.Value.MaxGiveItemCount;

        if (arguments.Count < 1 || arguments.Count > max)
            return CommandResult.Fail(BAD_COUNT, max.ToString(CultureInfo.InvariantCulture));

        if (definitionProvider.TryGetDefinitionByName(arguments.Furni) is not { } definition)
            return CommandResult.Fail(UNKNOWN_FURNI, arguments.Furni);

        var selection = await ctx.SelectAsync(arguments.Who, ct);

        if (selection.Failure is { } failure)
            return failure;

        var count = arguments.Count.ToString(CultureInfo.InvariantCulture);
        var batch = await ctx.ExecuteBatchAsync(
            selection.Players,
            arguments.Count,
            async (player, token) =>
                await grainFactory
                    .GetInventoryGrain(player.Id)
                    .GrantFurnitureAsync(definition.Id, null, token)
                    is not null,
            ct
        );

        await Task.WhenAll(
            batch
                .Targets.Where(x => x.Succeeded > 0)
                .Select(x =>
                    ctx.NotifyAsync(
                        x.PlayerId,
                        "command.giveitem.notice",
                        "A moderator added %0% item(s) of %1% to your inventory.",
                        [x.Succeeded.ToString(CultureInfo.InvariantCulture), definition.Name],
                        ct
                    )
                )
        );

        var completed = selection.IsSelector
            ? CommandResult.Done(
                GIVEN_MANY,
                batch.CompletedTargets.ToString(CultureInfo.InvariantCulture),
                count,
                definition.Name
            )
            : CommandResult.Done(GIVEN, selection.Players[0].Name, count, definition.Name);

        return batch.ToResult(
            completed,
            selection.IsSelector
                ? default
                : CommandResult.Fail(FAILED, selection.Players[0].Name, count, definition.Name)
        );
    }
}
