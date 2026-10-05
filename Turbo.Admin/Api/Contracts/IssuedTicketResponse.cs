using System;

namespace Turbo.Admin.Api.Contracts;

/// <summary>
/// A login ticket just issued, shown this once: the ticket, when it stops working (null for
/// never), whether it works more than once, and the client's login address with it, when the
/// hotel's is set.
/// </summary>
public sealed record IssuedTicketResponse(
    string Ticket,
    DateTime? ExpiresAtUtc,
    bool Reusable,
    string? LoginUrl
);
