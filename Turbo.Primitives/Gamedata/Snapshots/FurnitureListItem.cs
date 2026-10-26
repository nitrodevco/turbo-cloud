using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Gamedata.Enums;

namespace Turbo.Primitives.Gamedata.Snapshots;

/// <summary>A furniture definition as the list shows it: what it is, and its tags.</summary>
[GenerateSerializer, Immutable]
public sealed record FurnitureListItem
{
    [Id(0)]
    public required int Id { get; init; }

    [Id(1)]
    public required ProductType ProductType { get; init; }

    [Id(2)]
    public required string ClassName { get; init; }

    /// <summary>The name players see (furnidata's <c>name</c>).</summary>
    [Id(3)]
    public string? Name { get; init; }

    [Id(4)]
    public required int SpriteId { get; init; }

    [Id(5)]
    public string? FurniLine { get; init; }

    /// <summary>Furnidata's <c>category</c>.</summary>
    [Id(6)]
    public string? Category { get; init; }

    /// <summary>The bundle it is drawn from: its classname without the <c>*N</c> colour.</summary>
    [Id(7)]
    public required string AssetName { get; init; }

    [Id(8)]
    public required ImmutableArray<FurnitureTag> Tags { get; init; }

    /// <summary>Its bundle's revision; null without a bundle file, or for an upload.</summary>
    [Id(9)]
    public string? BundleRevision { get; init; }

    /// <summary>Its bundle's size in bytes; null without a bundle file.</summary>
    [Id(10)]
    public long? BundleSize { get; init; }

    /// <summary>Where its bundle came from; null without a bundle file.</summary>
    [Id(11)]
    public AssetBundleSource? BundleSource { get; init; }

    /// <summary>The logic the server runs for it (the definition's <c>logic</c>).</summary>
    [Id(12)]
    public required string Logic { get; init; }

    /// <summary>
    /// The logic the server has for it, by its classname or by the logic its asset file names;
    /// null when the server has none but the default.
    /// </summary>
    [Id(13)]
    public string? MatchingLogic { get; init; }
}
