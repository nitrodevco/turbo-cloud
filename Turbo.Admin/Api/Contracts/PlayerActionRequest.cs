namespace Turbo.Admin.Api.Contracts;

/// <summary>
/// Something to do to a player from the panel: <c>ban</c>, <c>unban</c>, <c>silence</c>,
/// <c>unsilence</c>, <c>tradelock</c>, <c>untradelock</c>, <c>disconnect</c>, <c>warn</c>,
/// <c>alert</c>, <c>give</c>, <c>givebadge</c>, <c>takebadge</c> or <c>giveitem</c>, with what it
/// takes: a duration (30m, 12h, 7d, 2w, perm), a reason or message, a currency and an amount (for
/// <c>giveitem</c>, the count), a badge code, and a furniture's class name.
/// </summary>
public sealed record PlayerActionRequest(
    string? Action,
    string? Duration,
    string? Reason,
    string? Currency,
    int? Amount,
    string? Badge = null,
    string? Furni = null
);
