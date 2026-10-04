using System;
using Orleans;
using Turbo.Primitives.Players;

namespace Turbo.Primitives.Admin.Snapshots;

/// <summary>An admin panel session: whose it is and when it ends.</summary>
[GenerateSerializer, Immutable]
public sealed record AdminSessionSnapshot
{
    [Id(0)]
    public required PlayerId PlayerId { get; init; }

    [Id(1)]
    public required DateTime ExpiresAtUtc { get; init; }
}
