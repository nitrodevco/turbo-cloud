using System;
using Orleans;
using Turbo.Primitives.Gamedata.Enums;

namespace Turbo.Primitives.Gamedata.Snapshots;

/// <summary>A set of gamedata changes made together, and rolled back together.</summary>
[GenerateSerializer, Immutable]
public sealed record GamedataChangeSetSnapshot
{
    [Id(0)]
    public required int Id { get; init; }

    [Id(1)]
    public required GamedataChangeKind Kind { get; init; }

    [Id(2)]
    public required string Summary { get; init; }

    /// <summary>The staff member who made it; null for one the server made on its own.</summary>
    [Id(3)]
    public int? PlayerId { get; init; }

    /// <summary>The Habbo release an import took in.</summary>
    [Id(4)]
    public int? ReleaseId { get; init; }

    /// <summary>The set a rollback undid.</summary>
    [Id(5)]
    public int? RevertsId { get; init; }

    /// <summary>The rollback that undid this set, once one has.</summary>
    [Id(6)]
    public int? RolledBackById { get; init; }

    [Id(7)]
    public required int ChangeCount { get; init; }

    [Id(8)]
    public required DateTime CreatedAt { get; init; }
}
