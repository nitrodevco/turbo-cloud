using System;

namespace Turbo.Admin.Api.Contracts;

/// <summary>
/// One change to a wallet: when, whose, which currency, how much (negative when it left), the
/// cause and what it names, who did it (null for the server), and the other side of the move:
/// <c>none</c> (made or spent), <c>wallet</c> (another player's), <c>offer</c> (a marketplace
/// offer), <c>chest</c> (a wired chest). Names are null for a player who is gone.
/// </summary>
public sealed record CurrencyLogEntry(
    long Id,
    DateTime AtUtc,
    int PlayerId,
    string? PlayerName,
    string Currency,
    long Amount,
    string Cause,
    string? CauseRef,
    int? ActorId,
    string? ActorName,
    string Other,
    int? OtherPlayerId,
    string? OtherPlayerName,
    long? OtherPlaceId
);

/// <summary>A currency to narrow the log to, by its currency type id.</summary>
public sealed record CurrencyLogCurrency(int TypeId, string Name);

/// <summary>What came into and went out of a player's wallet in one currency, across the filter.</summary>
public sealed record CurrencyLogTotal(string Currency, long In, long Out);

/// <summary>
/// A run of the currency log, newest first, with no total; whether the ledger is being kept now
/// (movements recorded and the ledger on); the currencies to narrow it to; and, for one player,
/// their totals per currency.
/// </summary>
public sealed record CurrencyLogResponse(
    int PageSize,
    bool HasOlder,
    bool HasNewer,
    bool Recording,
    CurrencyLogCurrency[] Currencies,
    CurrencyLogEntry[] Entries,
    CurrencyLogTotal[]? Totals
);
