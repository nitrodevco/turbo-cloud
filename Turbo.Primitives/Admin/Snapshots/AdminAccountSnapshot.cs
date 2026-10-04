using System.Collections.Immutable;
using Orleans;

namespace Turbo.Primitives.Admin.Snapshots;

/// <summary>A player's admin panel passkeys, as the account page shows them. None means no way in.</summary>
[GenerateSerializer, Immutable]
public sealed record AdminAccountSnapshot
{
    [Id(0)]
    public required ImmutableArray<AdminPasskeySnapshot> Passkeys { get; init; }
}
