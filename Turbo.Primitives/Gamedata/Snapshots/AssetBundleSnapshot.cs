using System;
using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Gamedata.Enums;

namespace Turbo.Primitives.Gamedata.Snapshots;

/// <summary>A bundle the hotel keeps, as its row says, and whether the hotel names it.</summary>
[GenerateSerializer, Immutable]
public sealed record AssetBundleSnapshot
{
    [Id(0)]
    public required AssetBundleKind Kind { get; init; }

    [Id(1)]
    public required string Name { get; init; }

    /// <summary>The revision it was taken at; null for an upload.</summary>
    [Id(2)]
    public string? Revision { get; init; }

    [Id(3)]
    public required AssetBundleSource Source { get; init; }

    /// <summary>Its SHA-1 in lowercase hex; null while it has no file.</summary>
    [Id(4)]
    public string? Hash { get; init; }

    [Id(5)]
    public required long Size { get; init; }

    /// <summary>The ids that load it: the effects sharing an effect's library, a pet's type.</summary>
    [Id(6)]
    public required ImmutableArray<int> Ids { get; init; }

    /// <summary>Why it could not be downloaded or converted.</summary>
    [Id(7)]
    public string? Error { get; init; }

    [Id(8)]
    public required DateTime UpdatedAt { get; init; }

    /// <summary>
    /// Whether the hotel names it: a furniture definition for a furniture, a breed's type for a
    /// pet. An effect is always used.
    /// </summary>
    [Id(9)]
    public required bool Used { get; init; }
}
