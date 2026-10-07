using System;
using Orleans;

namespace Turbo.Primitives.Gamedata.Snapshots;

/// <summary>
/// One of Habbo's releases as the hotel found it: the revision its external variables named and
/// the furniture data it served then.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record HabboReleaseSnapshot
{
    [Id(0)]
    public required int Id { get; init; }

    /// <summary>The Habbo hotel it came from (<c>com</c>, <c>nl</c>, ...).</summary>
    [Id(1)]
    public required string Domain { get; init; }

    /// <summary>The <c>flash-assets-&lt;revision&gt;</c> of its <c>flash.client.url</c>.</summary>
    [Id(2)]
    public required string Revision { get; init; }

    /// <summary>SHA-1 of the furniture data as downloaded.</summary>
    [Id(3)]
    public required string FurnitureDataHash { get; init; }

    [Id(4)]
    public required int FurnitureCount { get; init; }

    [Id(5)]
    public required DateTime FoundAt { get; init; }

    /// <summary>The last check that found Habbo still serving it.</summary>
    [Id(6)]
    public required DateTime CheckedAt { get; init; }

    /// <summary>When its furniture was taken in; null until it is.</summary>
    [Id(7)]
    public DateTime? ImportedAt { get; init; }
}
