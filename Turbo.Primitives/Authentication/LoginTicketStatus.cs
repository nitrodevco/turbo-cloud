using System;

namespace Turbo.Primitives.Authentication;

/// <summary>
/// A player's login ticket as it stands, without the ticket itself: when it stops working (null
/// for never), whether it works more than once, and whether it has run out.
/// </summary>
public sealed record LoginTicketStatus(DateTime? ExpiresAtUtc, bool Reusable, bool Expired);
