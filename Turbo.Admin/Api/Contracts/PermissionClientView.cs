using System.Collections.Immutable;

namespace Turbo.Admin.Api.Contracts;

/// <summary>What the client is told about a player: the security level, two flags and the perks allowed.</summary>
public sealed record PermissionClientView(
    string SecurityLevel,
    int SecurityLevelValue,
    bool IsAmbassador,
    bool IsModerator,
    ImmutableArray<string> PerksAllowed
);
