using Orleans;
using Turbo.Primitives.Gamedata.Enums;

namespace Turbo.Primitives.Gamedata.Snapshots;

/// <summary>
/// One thing a check of the bundles found: as its list says it (<see cref="Label"/>), and the
/// bundle it is about when one is named (an effect or pet type has none).
/// </summary>
[GenerateSerializer, Immutable]
public sealed record AssetCheckFinding
{
    /// <summary>The check that found it: <c>furniture-missing</c>, <c>failed</c>, ...</summary>
    [Id(0)]
    public required string CheckId { get; init; }

    [Id(1)]
    public required AssetCheckSeverity Severity { get; init; }

    [Id(2)]
    public required string Label { get; init; }

    [Id(3)]
    public AssetBundleKind? Kind { get; init; }

    [Id(4)]
    public string? Name { get; init; }

    /// <summary>Why, when the check knows: a library's error.</summary>
    [Id(5)]
    public string? Detail { get; init; }
}
