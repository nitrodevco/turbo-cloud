using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Furniture.Enums;

namespace Turbo.Primitives.Gamedata.Snapshots;

/// <summary>
/// A furniture definition as a bundle's own data says it would be, to edit before it is made:
/// what the asset data holds (its size, ways to face, states, logic and visualization, colours)
/// and the fields read from it.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record FurnitureDraft
{
    /// <summary>The furniture bundle it is drawn from, which is its classname.</summary>
    [Id(0)]
    public required string Bundle { get; init; }

    /// <summary>Floor, unless its logic or visualization is one only wall items have.</summary>
    [Id(1)]
    public required ProductType ProductType { get; init; }

    /// <summary>
    /// The fields to make it with, as a JSON object of furnidata and server keys (<c>classname</c>,
    /// <c>logic</c>, <c>name</c>, <c>xdim</c>, <c>ydim</c>, <c>height</c>, <c>states</c>).
    /// </summary>
    [Id(2)]
    public required string Fields { get; init; }

    /// <summary>What the bundle's asset data said, as JSON (a furniture asset file's info).</summary>
    [Id(3)]
    public required string File { get; init; }

    /// <summary>Its colours past the plain one: each can be a <c>classname*N</c> definition.</summary>
    [Id(4)]
    public required ImmutableArray<int> Colors { get; init; }

    /// <summary>The definitions already drawn from the bundle, by classname (<c>chair</c>, <c>chair*2</c>).</summary>
    [Id(5)]
    public required ImmutableArray<FurnitureDraftDefinition> Existing { get; init; }
}

/// <summary>A definition drawn from a bundle already.</summary>
[GenerateSerializer, Immutable]
public sealed record FurnitureDraftDefinition
{
    [Id(0)]
    public required int Id { get; init; }

    [Id(1)]
    public required ProductType ProductType { get; init; }

    [Id(2)]
    public required string ClassName { get; init; }
}
