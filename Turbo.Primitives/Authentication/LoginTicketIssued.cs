using System;

namespace Turbo.Primitives.Authentication;

/// <summary>
/// A login ticket just issued: the ticket itself, shown once to whoever issued it; when it stops
/// working (null for never); and whether it works more than once.
/// </summary>
public sealed record LoginTicketIssued(string Ticket, DateTime? ExpiresAtUtc, bool Reusable);
