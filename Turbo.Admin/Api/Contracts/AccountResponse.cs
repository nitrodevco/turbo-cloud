using System.Collections.Generic;

namespace Turbo.Admin.Api.Contracts;

/// <summary>The signed-in player's passkeys.</summary>
public sealed record AccountResponse(IReadOnlyList<AccountPasskey> Passkeys);
