namespace Turbo.Admin.Api.Contracts;

/// <summary>
/// Something to do to a player from the panel: <c>ban</c>, <c>unban</c>, <c>silence</c>,
/// <c>unsilence</c>, <c>tradelock</c>, <c>untradelock</c>, <c>disconnect</c>, <c>warn</c>,
/// <c>alert</c>, <c>give</c>, <c>givebadge</c>, <c>takebadge</c>, <c>giveitem</c> or <c>gift</c>,
/// with what it takes: a duration (30m, 12h, 7d, 2w, perm), a reason or message, a currency and an
/// amount (for <c>giveitem</c>, the count), a badge code (for <c>gift</c>, one given as the
/// present is opened, or none), a furniture's class name, and for <c>gift</c> the note on its tag.
/// </summary>
public sealed record PlayerActionRequest(
    string? Action,
    string? Duration,
    string? Reason,
    string? Currency,
    int? Amount,
    string? Badge = null,
    string? Furni = null,
    string? Note = null
);
