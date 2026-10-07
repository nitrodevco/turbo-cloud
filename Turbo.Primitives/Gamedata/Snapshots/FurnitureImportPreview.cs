using System.Collections.Immutable;
using Orleans;

namespace Turbo.Primitives.Gamedata.Snapshots;

/// <summary>What taking in a release's furniture would do, before anything is written.</summary>
[GenerateSerializer, Immutable]
public sealed record FurnitureImportPreview
{
    [Id(0)]
    public required HabboReleaseSnapshot Release { get; init; }

    [Id(1)]
    public required int Added { get; init; }

    [Id(2)]
    public required int Updated { get; init; }

    /// <summary>Items where Habbo changed only what the hotel had changed itself.</summary>
    [Id(3)]
    public required int Kept { get; init; }

    [Id(4)]
    public required int Unchanged { get; init; }

    /// <summary>The items it touches, as many as the preview limit allows.</summary>
    [Id(5)]
    public required ImmutableArray<FurnitureImportItem> Items { get; init; }

    /// <summary>Whether more items are touched than <see cref="Items"/> lists.</summary>
    [Id(6)]
    public required bool Truncated { get; init; }

    /// <summary>
    /// Furniture asset files taking it in reads first: those of its items not read yet, or that
    /// failed before. The comparison above uses what was read already.
    /// </summary>
    [Id(7)]
    public required int FilesToRead { get; init; }
}
