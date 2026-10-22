using System;
using Orleans;
using Turbo.Primitives.Players;

namespace Turbo.Primitives.Admin.Snapshots;

/// <summary>An admin panel session: whose it is, when it began and when it ends.</summary>
[GenerateSerializer, Immutable]
public sealed record AdminSessionSnapshot
{
    [Id(0)]
    public required PlayerId PlayerId { get; init; }

    [Id(1)]
    public required DateTime ExpiresAtUtc { get; init; }

    /// <summary>What a staff member names the session by to end it: never its token.</summary>
    [Id(2)]
    public int Id { get; init; }

    [Id(3)]
    public DateTime CreatedAtUtc { get; init; }
}
