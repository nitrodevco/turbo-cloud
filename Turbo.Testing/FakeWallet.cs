using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Contracts.Hotel;
using Turbo.Primitives.Players;

namespace Turbo.Testing;

/// <summary>One balance change a <see cref="FakeWallet"/> made: positive a credit, negative a debit.</summary>
public sealed record FakeMovement(
    PlayerId Player,
    string Currency,
    int Amount,
    string Reference,
    string Cause
);

/// <summary>
/// The wallets of a <see cref="FakeHotel"/>, keeping the running hotel's rules: currencies by name
/// in any case (<c>credits</c>, <c>duckets</c> and <c>diamonds</c> to start with), one change per
/// reference (references compared in any case, as the hotel's database does), credits and debits
/// apart, a debit all or nothing, and no receipt for a refused change.
/// </summary>
public sealed class FakeWallet : IHotelWallet
{
    private readonly FakeHotel _hotel;
    private readonly HashSet<string> _currencies = new(StringComparer.OrdinalIgnoreCase)
    {
        "credits",
        "duckets",
        "diamonds",
    };
    private readonly Dictionary<(PlayerId, string), int> _balances = new(new BalanceKey());
    private readonly HashSet<(PlayerId, string)> _credited = new(new BalanceKey());
    private readonly HashSet<(PlayerId, string)> _debited = new(new BalanceKey());
    private readonly List<FakeMovement> _movements = [];
    private readonly FakeFaults _faults = new();

    internal FakeWallet(FakeHotel hotel) => _hotel = hotel;

    /// <summary>Every change made, in order.</summary>
    public IReadOnlyList<FakeMovement> Movements => _movements;

    public void AddCurrency(string name) => _currencies.Add(name);

    /// <summary>
    /// The next <paramref name="count"/> credits or debits fail: the balance stays and no receipt
    /// is kept, as when the hotel's write fails. Each answers <see cref="WalletOutcome.Refused"/>,
    /// or throws <paramref name="throws"/> when given (a crash mid-call). A retry under the same
    /// reference afterwards goes through.
    /// </summary>
    public void FailNext(int count = 1, Exception? throws = null) =>
        _faults.FailNext(count, throws);

    /// <summary>Sets a balance as the test needs it (no movement).</summary>
    public void Set(PlayerId player, string currency, int amount)
    {
        if (!CurrencyExists(currency))
            throw new ArgumentException(
                $"The fake hotel has no currency {currency}: add it first."
            );

        _balances[(player, currency)] = amount;
    }

    public int Balance(PlayerId player, string currency) =>
        _balances.GetValueOrDefault((player, currency));

    public bool CurrencyExists(string currency) =>
        !string.IsNullOrWhiteSpace(currency) && _currencies.Contains(currency);

    public Task<int> BalanceAsync(PlayerId player, string currency, CancellationToken ct) =>
        Task.FromResult(CurrencyExists(currency) ? Balance(player, currency) : 0);

    public Task<WalletOutcome> CreditAsync(
        PlayerId player,
        string currency,
        int amount,
        string reference,
        string cause,
        CancellationToken ct
    ) => Task.FromResult(Change(player, currency, amount, reference, cause, _credited));

    public Task<WalletOutcome> DebitAsync(
        PlayerId player,
        string currency,
        int amount,
        string reference,
        string cause,
        CancellationToken ct
    ) => Task.FromResult(Change(player, currency, -amount, reference, cause, _debited, amount));

    /// <summary>The player was credited <paramref name="amount"/> of the currency in all.</summary>
    public void AssertCredited(PlayerId player, string currency, int amount)
    {
        var total = Total(player, currency, credits: true);

        if (total != amount)
            throw new HotelAssertionException(
                $"Expected player {player} credited {amount} {currency} in all; they were credited {total} ({FakeHotelText.List(Of(player))})."
            );
    }

    /// <summary>The player was debited <paramref name="amount"/> of the currency in all.</summary>
    public void AssertDebited(PlayerId player, string currency, int amount)
    {
        var total = Total(player, currency, credits: false);

        if (total != amount)
            throw new HotelAssertionException(
                $"Expected player {player} debited {amount} {currency} in all; they were debited {total} ({FakeHotelText.List(Of(player))})."
            );
    }

    /// <summary>Nothing of the player's balances changed.</summary>
    public void AssertUntouched(PlayerId player)
    {
        if (Of(player).Count > 0)
            throw new HotelAssertionException(
                $"Expected no change to player {player}'s wallet; it had {FakeHotelText.List(Of(player))}."
            );
    }

    internal bool Holds(PlayerId player, string currency) =>
        _balances.ContainsKey((player, currency));

    private WalletOutcome Change(
        PlayerId player,
        string currency,
        int delta,
        string reference,
        string cause,
        HashSet<(PlayerId, string)> receipts,
        int? asked = null
    )
    {
        HotelReference.ThrowIfInvalid(reference);

        if ((asked ?? delta) <= 0 || !CurrencyExists(currency) || !_hotel.IsPlayer(player))
            return WalletOutcome.Refused;

        if (receipts.Contains((player, reference)))
            return WalletOutcome.AlreadyDone;

        if (_faults.Trip())
            return WalletOutcome.Refused;

        var balance = (long)Balance(player, currency) + delta;

        if (balance is < 0 or > int.MaxValue)
            return WalletOutcome.Refused;

        _balances[(player, currency)] = (int)balance;
        receipts.Add((player, reference));
        _movements.Add(new(player, currency, delta, reference, cause));

        return WalletOutcome.Done;
    }

    private List<FakeMovement> Of(PlayerId player) =>
        [.. _movements.Where(x => x.Player == player)];

    private int Total(PlayerId player, string currency, bool credits) =>
        Of(player)
            .Where(x => x.Currency.Equals(currency, StringComparison.OrdinalIgnoreCase))
            .Where(x => credits ? x.Amount > 0 : x.Amount < 0)
            .Sum(x => Math.Abs(x.Amount));

    private sealed class BalanceKey : IEqualityComparer<(PlayerId, string)>
    {
        public bool Equals((PlayerId, string) x, (PlayerId, string) y) =>
            x.Item1 == y.Item1
            && string.Equals(x.Item2, y.Item2, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((PlayerId, string) obj) =>
            HashCode.Combine(obj.Item1, StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Item2));
    }
}
