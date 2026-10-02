using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Operations.Configuration;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Players.Providers;
using Turbo.Primitives.Players.Wallet;

namespace Turbo.Operations.Commands;

public sealed record GiveArguments(
    [Selectors(PermissionNodes.Command.GIVE_MASS)] PlayerTarget Who,
    [Suggest(SuggestionSources.CURRENCIES)] string Currency,
    int Amount
);

/// <summary>
/// <c>:give name credits 500</c>. Adds to a player's balance, or takes from it with a negative
/// amount. One command for every currency, named as the hotel's currency types are. <c>@room</c>
/// and <c>@online</c> need <c>command.give.mass</c> and are always logged. The amount is capped
/// by <c>Turbo:Operations:MaxCurrencyAmount</c>, so a typo is not the hotel's economy.
/// </summary>
[Command(
    "give",
    Description = "Add to, or take from, a player's balance",
    Category = CommandCategories.SUPPORT
)]
[RequiresPermission(PermissionNodes.Command.GIVE)]
public sealed class GiveCommand(
    IGrainFactory grainFactory,
    ICurrencyTypeProvider currencyTypeProvider,
    IOptions<OperationsConfig> config
) : IOperatorCommand<GiveArguments>
{
    private const string GIVEN = "given";
    private const string GIVEN_MANY = "given_many";
    private const string FAILED = "failed";
    private const string BAD_AMOUNT = "bad_amount";
    private const string UNKNOWN_CURRENCY = "unknown_currency";

    public IReadOnlyDictionary<string, string> DefaultTexts { get; } =
        new Dictionary<string, string>
        {
            [GIVEN] = "%0% was given %1% %2%.",
            [GIVEN_MANY] = "%0% players got %1% %2%.",
            [FAILED] = "%0% could not be given %1% %2%, a balance can't go below nothing.",
            [BAD_AMOUNT] =
                "The amount must be at least 1, or -1 and less to take, and at most %0% either way.",
            [UNKNOWN_CURRENCY] = "There is no currency called %0%. The currencies are: %1%.",
        };

    public async ValueTask<CommandResult> ExecuteAsync(
        IOperatorCommandContext ctx,
        GiveArguments arguments,
        CancellationToken ct
    )
    {
        var max = config.Value.MaxCurrencyAmount;

        if (arguments.Amount == 0 || Math.Abs((long)arguments.Amount) > max)
            return CommandResult.Fail(BAD_AMOUNT, max.ToString(CultureInfo.InvariantCulture));

        if (!currencyTypeProvider.TryGetCurrencyKindByName(arguments.Currency, out var kind))
            return CommandResult.Fail(
                UNKNOWN_CURRENCY,
                arguments.Currency,
                string.Join(", ", currencyTypeProvider.GetEnabledCurrencyNames())
            );

        var selection = await ctx.SelectAsync(arguments.Who, ct);

        if (selection.Failure is { } failure)
            return failure;

        var amount = arguments.Amount.ToString(CultureInfo.InvariantCulture);
        var batch = await ctx.ExecuteBatchAsync(
            selection.Players,
            1,
            async (player, token) =>
            {
                var wallet = grainFactory.GetPlayerWalletGrain(player.Id);
                return arguments.Amount > 0
                    ? await wallet.CreditRewardAsync(kind, arguments.Amount, token)
                    : (
                        await wallet.TryDebitAsync(
                            [
                                new WalletDebitRequest
                                {
                                    CurrencyKind = kind,
                                    Amount = -arguments.Amount,
                                },
                            ],
                            token
                        )
                    ).Succeeded;
            },
            ct
        );

        // Positive rewards carry a durable wallet receipt and use the native corner toast.
        // Administrative deductions retain their explicit adjustment notice.
        if (arguments.Amount < 0)
            await Task.WhenAll(
                batch
                    .Targets.Where(x => x.Succeeded > 0)
                    .Select(x =>
                        ctx.NotifyAsync(
                            x.PlayerId,
                            "command.give.notice",
                            "A moderator adjusted your balance by %0% %1%.",
                            [amount, arguments.Currency],
                            ct
                        )
                    )
            );

        var completed = selection.IsSelector
            ? CommandResult.Done(
                GIVEN_MANY,
                batch.CompletedTargets.ToString(CultureInfo.InvariantCulture),
                amount,
                arguments.Currency
            )
            : CommandResult.Done(GIVEN, selection.Players[0].Name, amount, arguments.Currency);

        return batch.ToResult(
            completed,
            selection.IsSelector
                ? default
                : CommandResult.Fail(FAILED, selection.Players[0].Name, amount, arguments.Currency)
        );
    }
}
