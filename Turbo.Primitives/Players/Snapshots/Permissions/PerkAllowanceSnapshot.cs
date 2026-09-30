using Orleans;
using Turbo.Primitives.Players.Enums;

namespace Turbo.Primitives.Players.Snapshots.Permissions;

/// <summary>One entry of <c>PerkAllowances</c>, as the permission projection decides it.</summary>
[GenerateSerializer, Immutable]
public sealed record PerkAllowanceSnapshot
{
    [Id(0)]
    public required PlayerPerkFlags Perk { get; init; }

    [Id(1)]
    public required bool IsAllowed { get; init; }

    /// <summary>The refusal text sent with it; empty when the node registers none.</summary>
    [Id(2)]
    public required string Refusal { get; init; }
}
