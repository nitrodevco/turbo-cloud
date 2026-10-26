using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Gamedata.Enums;

namespace Turbo.Primitives.Gamedata.Snapshots;

/// <summary>
/// A window template (<c>templates.json</c>, keyed <c>&lt;library&gt;/&lt;asset&gt;</c>) that differs
/// from the target's copy to the hotel's. For a changed one: its elements added, removed and changed
/// (by name, children aside), and the text keys its elements use (<c>${catalog.title}</c>) added and
/// removed.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record AssetWindowDiff(
    [property: Id(0)] string Id,
    [property: Id(1)] AssetDiffChange Change,
    [property: Id(2)] ImmutableArray<string> ElementsAdded,
    [property: Id(3)] ImmutableArray<string> ElementsRemoved,
    [property: Id(4)] ImmutableArray<string> ElementsChanged,
    [property: Id(5)] ImmutableArray<string> TextKeysAdded,
    [property: Id(6)] ImmutableArray<string> TextKeysRemoved
);
