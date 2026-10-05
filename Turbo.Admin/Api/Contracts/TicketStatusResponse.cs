using System;

namespace Turbo.Admin.Api.Contracts;

/// <summary>
/// A player's login ticket as it stands, never the ticket itself: whether they have one, when it
/// stops working (null for never), whether it works more than once, and whether it has run out.
/// </summary>
public sealed record TicketStatusResponse(
    bool HasTicket,
    DateTime? ExpiresAtUtc,
    bool Reusable,
    bool Expired
);
