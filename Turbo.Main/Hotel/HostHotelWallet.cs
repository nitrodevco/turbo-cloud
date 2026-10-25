using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Contracts.Hotel;
using Turbo.Primitives.Movements;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Providers;
using Turbo.Primitives.Players.Wallet;

namespace Turbo.Main.Hotel;

/// <summary>
/// <see cref="IHotelWallet"/> over the wallet grain's referenced credit and debit
/// (<c>docs/wallet-idempotent-credit.md</c>) and the hotel's currency types by name.
/// </summary>
internal sealed class HostHotelWallet(IGrainFactory grains, ICurrencyTypeProvider currencies)
    : IHotelWallet
{
    public bool CurrencyExists(string currency) =>
        !string.IsNullOrWhiteSpace(currency)
        && currencies.TryGetCurrencyKindByName(currency, out _);

    public async Task<int> BalanceAsync(PlayerId player, string currency, CancellationToken ct) =>
        currencies.TryGetCurrencyKindByName(currency, out var kind)
            ? await grains
                .GetPlayerWalletGrain(player)
                .GetAmountForCurrencyAsync(kind, ct)
                .ConfigureAwait(false)
            : 0;

    public async Task<WalletOutcome> CreditAsync(
        PlayerId player,
        string currency,
        int amount,
        string reference,
        string cause,
        CancellationToken ct
    )
    {
        HotelReference.ThrowIfInvalid(reference);

        if (amount <= 0 || !currencies.TryGetCurrencyKindByName(currency, out var kind))
            return WalletOutcome.Refused;

        return await grains
            .GetPlayerWalletGrain(player)
            .CreditAsync(kind, amount, reference, MovementCause.New(cause, player, reference), ct)
            .ConfigureAwait(false) switch
        {
            WalletCreditResult.Applied => WalletOutcome.Done,
            WalletCreditResult.AlreadyApplied => WalletOutcome.AlreadyDone,
            _ => WalletOutcome.Refused,
        };
    }

    public async Task<WalletOutcome> DebitAsync(
        PlayerId player,
        string currency,
        int amount,
        string reference,
        string cause,
        CancellationToken ct
    )
    {
        HotelReference.ThrowIfInvalid(reference);

        if (amount <= 0 || !currencies.TryGetCurrencyKindByName(currency, out var kind))
            return WalletOutcome.Refused;

        var result = await grains
            .GetPlayerWalletGrain(player)
            .TryDebitAsync(
                [new WalletDebitRequest { CurrencyKind = kind, Amount = amount }],
                reference,
                MovementCause.New(cause, player, reference),
                ct
            )
            .ConfigureAwait(false);

        return !result.Succeeded ? WalletOutcome.Refused
            : result.AlreadyApplied ? WalletOutcome.AlreadyDone
            : WalletOutcome.Done;
    }
}
