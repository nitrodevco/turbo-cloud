namespace Turbo.Admin.Api.Contracts;

/// <summary>
/// Something to do to a player from the panel: <c>ban</c>, <c>unban</c>, <c>silence</c>,
/// <c>unsilence</c>, <c>tradelock</c>, <c>untradelock</c>, <c>disconnect</c>, <c>warn</c>,
/// <c>alert</c> or <c>give</c>, with what it takes: a duration (30m, 12h, 7d, 2w, perm), a reason
/// or message, a currency and an amount.
/// </summary>
public sealed record PlayerActionRequest(
    string? Action,
    string? Duration,
    string? Reason,
    string? Currency,
    int? Amount
);
