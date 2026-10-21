using System;
using Orleans;

namespace Turbo.Primitives.Gamedata.Snapshots;

/// <summary>One version of Habbo's external variables as a check found it.</summary>
[GenerateSerializer, Immutable]
public sealed record HabboVariableVersionSnapshot
{
    [Id(0)]
    public required int Id { get; init; }

    [Id(1)]
    public required string Domain { get; init; }

    /// <summary>SHA-1 of the variables as downloaded.</summary>
    [Id(2)]
    public required string Hash { get; init; }

    [Id(3)]
    public required int VariableCount { get; init; }

    [Id(4)]
    public required DateTime FoundAt { get; init; }

    [Id(5)]
    public required DateTime CheckedAt { get; init; }

    /// <summary>When any of it was last taken in; null until some is.</summary>
    [Id(6)]
    public DateTime? ImportedAt { get; init; }

    /// <summary>The hash Habbo's <c>/gamedata/hashes</c> lists the file under; null when unread.</summary>
    [Id(7)]
    public string? HabboHash { get; init; }
}
