using System.Collections.Generic;

namespace Turbo.Admin.Api.Contracts;

/// <summary>The signed-in player's passkeys and the sessions they are signed in with.</summary>
public sealed record AccountResponse(
    IReadOnlyList<AccountPasskey> Passkeys,
    IReadOnlyList<AccountSession> Sessions
);
