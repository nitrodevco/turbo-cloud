using Orleans;

namespace Turbo.Primitives.Gamedata.Snapshots;

/// <summary>One of the client's external variables.</summary>
[GenerateSerializer, Immutable]
public sealed record VariableEntrySnapshot
{
    [Id(0)]
    public required string Key { get; init; }

    /// <summary>The value as JSON: <c>"text"</c>, <c>true</c>, <c>120</c>, <c>[1, 2]</c>.</summary>
    [Id(1)]
    public required string Value { get; init; }
}
