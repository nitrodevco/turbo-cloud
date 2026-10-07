using System.Collections.Immutable;
using Orleans;

namespace Turbo.Primitives.Gamedata.Snapshots;

/// <summary>What taking in a version of Habbo's figure data would do, before anything is written.</summary>
[GenerateSerializer, Immutable]
public sealed record FigureImportPreview
{
    [Id(0)]
    public required HabboFigureVersionSnapshot Version { get; init; }

    [Id(1)]
    public required int Added { get; init; }

    [Id(2)]
    public required int Updated { get; init; }

    /// <summary>Records Habbo changed that the hotel had changed (or removed) itself.</summary>
    [Id(3)]
    public required int Kept { get; init; }

    [Id(4)]
    public required int Unchanged { get; init; }

    [Id(5)]
    public required ImmutableArray<FigureImportItem> Items { get; init; }

    [Id(6)]
    public required bool Truncated { get; init; }
}
