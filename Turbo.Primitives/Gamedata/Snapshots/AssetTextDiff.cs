using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Gamedata.Enums;

namespace Turbo.Primitives.Gamedata.Snapshots;

/// <summary>
/// One language's texts (<c>DefaultLocalizations_&lt;language&gt;.json</c>) that differ from the
/// target's copy to the hotel's: how many keys were added, removed and changed, and the first
/// <see cref="Edits"/> of them with their texts before and after.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record AssetTextDiff(
    [property: Id(0)] string Language,
    [property: Id(1)] int Added,
    [property: Id(2)] int Removed,
    [property: Id(3)] int Changed,
    [property: Id(4)] ImmutableArray<AssetTextEdit> Edits
);

/// <summary>A text key added, removed or changed, with its text in the target's copy and the hotel's.</summary>
[GenerateSerializer, Immutable]
public sealed record AssetTextEdit(
    [property: Id(0)] string Key,
    [property: Id(1)] AssetDiffChange Change,
    [property: Id(2)] string? Before,
    [property: Id(3)] string? After
);
