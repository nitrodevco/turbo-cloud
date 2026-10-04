using System;
using Orleans;

namespace Turbo.Primitives.Admin.Snapshots;

/// <summary>A passkey as its owner sees it in the panel: no key material.</summary>
[GenerateSerializer, Immutable]
public sealed record AdminPasskeySnapshot
{
    [Id(0)]
    public required int Id { get; init; }

    [Id(1)]
    public required string Name { get; init; }

    [Id(2)]
    public required DateTime CreatedAtUtc { get; init; }

    [Id(3)]
    public DateTime? LastUsedAtUtc { get; init; }
}
