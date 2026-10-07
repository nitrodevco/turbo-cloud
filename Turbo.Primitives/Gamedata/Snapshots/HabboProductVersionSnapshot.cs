using System;
using Orleans;

namespace Turbo.Primitives.Gamedata.Snapshots;

/// <summary>One version of Habbo's product data as a check found it.</summary>
[GenerateSerializer, Immutable]
public sealed record HabboProductVersionSnapshot
{
    [Id(0)]
    public required int Id { get; init; }

    [Id(1)]
    public required string Domain { get; init; }

    /// <summary>SHA-1 of the product data as downloaded.</summary>
    [Id(2)]
    public required string Hash { get; init; }

    [Id(3)]
    public required int ProductCount { get; init; }

    [Id(4)]
    public required DateTime FoundAt { get; init; }

    [Id(5)]
    public required DateTime CheckedAt { get; init; }

    /// <summary>When it was taken in; null until it is.</summary>
    [Id(6)]
    public DateTime? ImportedAt { get; init; }
}
