using System;
using System.Collections.Immutable;
using Orleans;

namespace Turbo.Primitives.Gamedata.Snapshots;

/// <summary>
/// What a sync from Habbo would download now, kind by kind, without downloading any of it: Habbo's
/// lists as last read, against the hotel's bundles as they are.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record AssetSyncPreview
{
    [Id(0)]
    public required string Domain { get; init; }

    /// <summary>The client revision Habbo serves (<c>flash-assets-...</c>).</summary>
    [Id(1)]
    public required string Revision { get; init; }

    /// <summary>When Habbo's lists were read.</summary>
    [Id(2)]
    public required DateTime ListedAt { get; init; }

    /// <summary>Furniture, clothing, effects and pets, in the order a sync takes them.</summary>
    [Id(3)]
    public required ImmutableArray<AssetSyncKindPreview> Kinds { get; init; }
}
