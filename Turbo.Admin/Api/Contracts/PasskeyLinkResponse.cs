using System;

namespace Turbo.Admin.Api.Contracts;

/// <summary>
/// A setup link for another player, to hand to them. <see cref="HasPanelAccess"/> says whether
/// they hold <c>admin.panel</c>; without it, the passkey works but signs them in to nothing.
/// </summary>
public sealed record PasskeyLinkResponse(
    string PlayerName,
    string Link,
    DateTime ExpiresAtUtc,
    bool ReplacesExisting,
    bool HasPanelAccess
);
